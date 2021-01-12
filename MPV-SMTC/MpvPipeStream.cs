using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using Microsoft.CSharp.RuntimeBinder;

using Serilog;

namespace MPVSMTC
{
    class MpvPipeStream
    {

        private record CommandObject
        {
            public dynamic[] command;
            public long request_id;
            public bool async;
        }

        private readonly NamedPipeClientStream PipeClient;
        private StreamWriter PipeWriter;
        private StreamReader PipeReader;
        private readonly int ConnectionTimeout;

        private readonly Dictionary<long, TaskCompletionSource<dynamic>> PromiseMap;
        private readonly Dictionary<string, HashSet<Action<dynamic>>> EventHandlerMap;
        private readonly Dictionary<string, HashSet<Action<dynamic>>> PropertyObserverMap;
        private readonly Dictionary<string, int> PropertyIDMap;
        

        public delegate void MPVPipeEvent();

        public event MPVPipeEvent OnDisconnect;
        public event MPVPipeEvent OnConnectionTimeout;

        public MpvPipeStream(string PipeName, int ConnectionTimeout = 3000)
        {
            this.PipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            this.PromiseMap = new Dictionary<long, TaskCompletionSource<dynamic>>();
            this.EventHandlerMap = new Dictionary<string, HashSet<Action<dynamic>>>();
            this.PropertyObserverMap = new Dictionary<string, HashSet<Action<dynamic>>>();
            this.PropertyIDMap = new Dictionary<string, int>();

            this.ConnectionTimeout = ConnectionTimeout;
        }

        public void Connect()
        {
            if (!Task.Run(() => { this.PipeClient.Connect(); }).Wait(ConnectionTimeout))
            {
                this.OnConnectionTimeout?.Invoke();
                return;
            }

            this.PipeClient.ReadMode = PipeTransmissionMode.Byte;

            this.PipeWriter = new StreamWriter(this.PipeClient)
            {
                AutoFlush = true
            };
            this.PipeReader = new StreamReader(this.PipeClient);

            
            this.ReadLineLoop();
            Log.Information("Connected to MPV");
        }

        private void WriteRawLine(string line)
        {
            lock (PipeWriter)
            {
                Log.Verbose("Writing Line: {0}", line);
                this.PipeWriter.WriteLine(line);
                Log.Verbose("Written Line: {0}", line);
            }
        }

        private async Task ReadLineLoop()
        {
            while (this.PipeClient.IsConnected)
            {
                string line = await this.PipeReader.ReadLineAsync();
                if (line is null || line.Length == 0)
                {
                    continue;
                }
                Log.Verbose("Received Line: {0}", line);

                dynamic parseresult = JsonConvert.DeserializeObject<dynamic>(line);
                bool parsed = false;

                try
                {
                    if (!parsed)
                    {
                        long req_id = parseresult.request_id;
                        lock (PromiseMap)
                        {
                            if (PromiseMap.ContainsKey(req_id))
                            {
                                PromiseMap[req_id].TrySetResult(parseresult);
                                PromiseMap.Remove(req_id);
                            }
                        }
                        parsed = true;
                    }
                }
                catch (RuntimeBinderException)
                {

                }

                try
                {
                    if (!parsed)
                    {
                        string event_name = parseresult.@event;
                        Log.Verbose("Handling event {0}", event_name);

                        List<Action<dynamic>> handler_list = new List<Action<dynamic>>();
                        lock (EventHandlerMap)
                        {
                            if (this.EventHandlerMap.ContainsKey(event_name))
                            {
                                foreach (var t in this.EventHandlerMap[event_name])
                                {
                                    handler_list.Add(t);
                                    
                                }
                            }
                        }
                        foreach (var t in handler_list)
                        {
                            await new TaskFactory().StartNew(t, parseresult);
                        }
                        parsed = true;
                    }
                }
                catch (RuntimeBinderException)
                {

                }

                if (!parsed)
                {
                    Log.Warning("Error parsing line: {0}", line);
                }
            }

            this.OnDisconnect?.Invoke();

        }

        private async Task WriteCommand(dynamic[] command, long rid)
        {
            var cmd_obj = new CommandObject { command = command, async = true, request_id = rid };
            StringWriter cmd_writer = new StringWriter();

            var serializer = JsonSerializer.CreateDefault();
            serializer.Serialize(cmd_writer, cmd_obj);
            var cmd_str = cmd_writer.ToString();

            Log.Verbose("Sending command: [{0}]", string.Join(",", command.Select(p => p.ToString())));

            await Task.Run(() => { this.WriteRawLine(cmd_str); });
        }

        public Task<dynamic> MakeAsyncRequest(dynamic[] command)
        {
            var rand = new Random();
            var promise = new TaskCompletionSource<dynamic>();

            long c_req_id;
            lock (PromiseMap)
            {
                while (this.PromiseMap.ContainsKey(c_req_id = rand.Next())) { }
                this.PromiseMap[c_req_id] = promise;
            }

            WriteCommand(command, c_req_id);
            return promise.Task;
        }

        public Task<dynamic> MakeAsyncRequest(string command)
        {
            string[] command_arr = command.Split(' ');
            return this.MakeAsyncRequest(command_arr);
        }

        public void AddEventHandler(string event_name, Action<dynamic> handler)
        {
            lock (EventHandlerMap)
            {
                Log.Debug("Adding event handler for: {0}", event_name);
                if (!EventHandlerMap.ContainsKey(event_name))
                {
                    EventHandlerMap[event_name] = new HashSet<Action<dynamic>>();
                }
                EventHandlerMap[event_name].Add(handler);
            }
        }

        public void RemoveEventHandler(string event_name, Action<dynamic> handler)
        {
            lock (EventHandlerMap)
            {
                Log.Debug("Removing event handler for: {0}", event_name);
                if (!EventHandlerMap.ContainsKey(event_name))
                {
                    return;
                }
                if (!EventHandlerMap[event_name].Contains(handler))
                {
                    return;
                }
                EventHandlerMap[event_name].Remove(handler);
                if (EventHandlerMap[event_name].Count == 0)
                {
                    EventHandlerMap.Remove(event_name);
                }
            }
        }

        private async void PropertyChangeEventRouter(dynamic e)
        {
            try
            {
                string property_name = e.name;
                Log.Verbose("Handling property {0} change", property_name);

                List<Action<dynamic>> handler_list = new List<Action<dynamic>>();

                lock (PropertyObserverMap)
                {
                    if (this.PropertyObserverMap.ContainsKey(property_name))
                    {
                        foreach (var t in this.PropertyObserverMap[property_name])
                        {
                            handler_list.Add(t);
                        }
                    }
                }

                foreach(var t in handler_list)
                {
                    await new TaskFactory().StartNew(t, e);
                }

            }
            catch(RuntimeBinderException)
            {

            }
        }

        public async void AddPropertyObserver(string property_name, Action<dynamic> handler) {
            lock (PropertyObserverMap)
            {
                Log.Debug("Adding observer for: {0}", property_name);
                var rand = new Random();

                if (!this.PropertyObserverMap.ContainsKey(property_name))
                {
                    this.PropertyObserverMap[property_name] = new HashSet<Action<dynamic>>();
                }
                this.PropertyObserverMap[property_name].Add(handler);

                lock (EventHandlerMap)
                {
                    if (!this.EventHandlerMap.ContainsKey("property-change"))
                    {
                        this.AddEventHandler("property-change", this.PropertyChangeEventRouter);
                    }
                }

                if (!this.PropertyIDMap.ContainsKey(property_name))
                {
                    this.PropertyIDMap[property_name] = rand.Next();
                    this.MakeAsyncRequest(new dynamic[] { "observe_property", this.PropertyIDMap[property_name], property_name });
                }
            }
        }

        public void RemovePropertyObserver(string property_name, Action<dynamic> handler) {
            lock (PropertyObserverMap)
            {
                Log.Debug("Removing observer for: {0}", property_name);
                if (!this.PropertyObserverMap.ContainsKey(property_name))
                {
                    return;
                }
                if (this.PropertyIDMap.Count == 0 || !this.PropertyIDMap.ContainsKey(property_name))
                {
                    return;
                }

                this.PropertyObserverMap[property_name].Remove(handler);

                if (this.PropertyObserverMap[property_name].Count == 0)
                {
                    this.PropertyObserverMap.Remove(property_name);
                }

                if (!this.PropertyObserverMap.ContainsKey(property_name) || this.PropertyObserverMap[property_name].Count == 0)
                {
                    this.MakeAsyncRequest(new dynamic[] { "unobserve_property", this.PropertyIDMap[property_name] });
                    this.PropertyIDMap.Remove(property_name);
                }

                if (this.PropertyIDMap.Count == 0)
                {
                    this.RemoveEventHandler("property-change", this.PropertyChangeEventRouter);
                }
            }
        }

    }
}