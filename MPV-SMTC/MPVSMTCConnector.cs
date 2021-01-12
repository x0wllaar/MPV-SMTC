using System;
using System.Threading.Tasks;
using System.IO;

using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage;
using Microsoft.CSharp.RuntimeBinder;

using Serilog;

namespace MPVSMTC
{
    class MPVSMTCConnector
    {        
        private readonly string pipe_path;

        private readonly MpvPipeStream mpv_stream;

        private readonly SystemMediaTransportControls controls;
        private readonly SystemMediaTransportControlsDisplayUpdater updater;
        private readonly MediaPlayer player;

        private readonly bool GetDataFromFiles;

        public event MpvPipeStream.MPVPipeEvent OnDisconnect;
        public event MpvPipeStream.MPVPipeEvent OnConnectionTimeout;

        public MPVSMTCConnector(string pipe_path, int ConnectionTimeout = 3000, bool GetDataFromFiles = false)
        {
            this.pipe_path = pipe_path;
            this.mpv_stream = new MpvPipeStream(this.pipe_path, ConnectionTimeout);
            this.mpv_stream.OnConnectionTimeout += () => { this.OnConnectionTimeout?.Invoke(); };
            this.mpv_stream.OnDisconnect += () => { this.OnDisconnect?.Invoke(); };

            this.player = new MediaPlayer();
            this.player.CommandManager.IsEnabled = false;

            this.controls = this.player.SystemMediaTransportControls;
            this.updater = controls.DisplayUpdater;

            this.controls.IsPlayEnabled = true;
            this.controls.IsPauseEnabled = true;
            this.controls.IsNextEnabled = true;
            this.controls.IsPreviousEnabled = true;
            this.controls.IsEnabled = false;
            this.controls.PlaybackStatus = MediaPlaybackStatus.Changing;

            this.GetDataFromFiles = GetDataFromFiles;

        }

        private async void PlayPauseObserver(dynamic e)
        {
            bool pause_state = MpvPipeStreamHelpers.GetPropertyDataFromResponse<bool>(e, true);
            if (pause_state)
            {
                this.controls.PlaybackStatus = MediaPlaybackStatus.Paused;
            }
            else
            {
                this.controls.PlaybackStatus = MediaPlaybackStatus.Playing;
            }
        }

        private async Task<string> GetCurrentFilePath(){
            dynamic file_path_obj = await mpv_stream.MakeAsyncRequest("get_property_string path");
            string file_path = MpvPipeStreamHelpers.GetPropertyDataFromResponse<string>(file_path_obj, "");

            dynamic workdir_obj = await mpv_stream.MakeAsyncRequest("get_property_string working-directory");
            string workdir = MpvPipeStreamHelpers.GetPropertyDataFromResponse<string>(workdir_obj, "");

            await mpv_stream.MakeAsyncRequest("get_property filtered-metadata");

            string full_path = "";
            try
            {
                full_path = Path.GetFullPath(Path.Combine(workdir, file_path));
            }
            catch (ArgumentNullException)
            {

            }
            if (full_path is null || full_path == "")
            {
                Log.Warning("Failed to get currently playing file path");
            }
            return full_path;
        }

        private async Task<bool> SetMetadataFromFile(string file_path, MediaPlaybackType file_type)
        {
            bool data_set = false;
            if (File.Exists(file_path))
            {
                var media_file = await StorageFile.GetFileFromPathAsync(file_path);
                await controls.DisplayUpdater.CopyFromFileAsync(file_type, media_file);
                controls.DisplayUpdater.Update();
                data_set = true;
            }
            if (!data_set)
            {
                Log.Warning("Failed to copy metadate from file {0}", file_path);
            }
            return data_set;
        }

        private async Task<MediaPlaybackType> GetCurrentMediaType()
        {
            bool audio_present = false;
            bool video_present = false;
            
            dynamic track_count_obj = await mpv_stream.MakeAsyncRequest("get_property track-list/count");
            int track_count = MpvPipeStreamHelpers.GetPropertyDataFromResponse<int>(track_count_obj, 0);
            for (int track_n = 0; track_n < track_count; track_n++)
            {
                string track_type_request = "track-list/" + track_n.ToString() + "/type";
                dynamic track_type_obj = await mpv_stream.MakeAsyncRequest(new string[] { "get_property", track_type_request });
                string track_type = MpvPipeStreamHelpers.GetPropertyDataFromResponse<string>(track_type_obj, "");
                
                if(track_type == "video")
                {
                    string track_alba_request = "track-list/" + track_n.ToString() + "/albumart";
                    dynamic track_alba_obj = await mpv_stream.MakeAsyncRequest(new string[] { "get_property", track_alba_request });
                    bool track_alba = MpvPipeStreamHelpers.GetPropertyDataFromResponse<bool>(track_alba_obj, false);
                    if (track_alba)
                    {
                        track_type = "albumart";
                    }
                }
                
                switch (track_type)
                {
                    case "audio":
                        audio_present = true;
                        break;
                    case "video":
                        video_present = true;
                        break;
                    default:
                        break;
                }
            }

            if(audio_present && video_present)
            {
                return MediaPlaybackType.Video;
            }
            if (audio_present)
            {
                return MediaPlaybackType.Music;
            }
            if (video_present)
            {
                return MediaPlaybackType.Image;
            }

            return MediaPlaybackType.Unknown;
        }

        private async void SetMetadata(dynamic e)
        {
            string current_file = await GetCurrentFilePath();

            dynamic test = e.data;
            if (test is null)
            {
                Log.Verbose("Cannot set metadata yet");
                return;
            }

            Log.Verbose("Guessing media type for current file");
            var playback_type = await GetCurrentMediaType();
            Log.Debug("Guessed media type for current file {0}", playback_type);
            
            if (playback_type == MediaPlaybackType.Unknown)
            {
                this.controls.IsEnabled = false;
                Log.Debug("Media type unknown, disabling controls");
                return;
            }
            else
            {

                if (!GetDataFromFiles || !(await SetMetadataFromFile(current_file, playback_type)))
                {
                    dynamic full_md_obj = await mpv_stream.MakeAsyncRequest("get_property filtered-metadata");

                    string artist = "";
                    try
                    {
                        artist = full_md_obj.data.Artist;
                    }
                    catch (RuntimeBinderException)
                    {
                        Log.Verbose("Failed to get artist from metadata");
                    }

                    string title = "";
                    try
                    {
                        title = full_md_obj.data.Title;
                    }
                    catch (RuntimeBinderException)
                    {
                        Log.Verbose("Failed to get title from metadata");
                    }

                    if (title is null || title == "")
                    {
                        dynamic title_obj = await mpv_stream.MakeAsyncRequest("get_property_string media-title");
                        title = MpvPipeStreamHelpers.GetPropertyDataFromResponse<string>(title_obj, "");
                        if (title == "")
                        {
                            Log.Verbose("Failed to get title from MPV");
                        }
                    }

                    updater.Type = playback_type;
                    switch (playback_type)
                    {
                        case MediaPlaybackType.Video:
                            this.updater.VideoProperties.Title = title;
                            this.updater.VideoProperties.Subtitle = artist;
                            break;
                        case MediaPlaybackType.Music:
                            this.updater.MusicProperties.Title = title;
                            this.updater.MusicProperties.Artist = artist;
                            break;
                        case MediaPlaybackType.Image:
                            this.updater.ImageProperties.Title = title;
                            this.updater.ImageProperties.Subtitle = artist;
                            break;
                        default:
                            break;
                    }
                    updater.Update();
                }
            }
        }

        private async void SetPlaylistButtons(dynamic e)
        {
            dynamic playlist_playing_pos_obj = await mpv_stream.MakeAsyncRequest("get_property playlist-playing-pos");
            int? playlist_playing_pos = MpvPipeStreamHelpers.GetPropertyDataFromResponse<int?>(playlist_playing_pos_obj, null);
            if (playlist_playing_pos is null || playlist_playing_pos < 0)
            {
                this.controls.IsNextEnabled = false;
                this.controls.IsPreviousEnabled = false;
                this.controls.IsEnabled = false;
                return;
            }
            
            if (!this.controls.IsEnabled)
            {
                this.controls.IsEnabled = true;
            }
            
            dynamic playlist_count_obj = await mpv_stream.MakeAsyncRequest("get_property playlist/count");
            int? playlist_count = MpvPipeStreamHelpers.GetPropertyDataFromResponse<int?>(playlist_count_obj, null);
            if (playlist_count is null || playlist_count < 0)
            {
                this.controls.IsNextEnabled = false;
                this.controls.IsPreviousEnabled = false;
                return;
            }

            if (playlist_playing_pos == 0 && playlist_count == 1)
            {
                this.controls.IsNextEnabled = false;
                this.controls.IsPreviousEnabled = false;
                return;
            }

            if (playlist_playing_pos == 0 && playlist_count > 0)
            {
                this.controls.IsNextEnabled = true;
                this.controls.IsPreviousEnabled = false;
                return;
            }


            if (playlist_playing_pos > 0 && playlist_playing_pos == playlist_count - 1)
            {
                this.controls.IsNextEnabled = false;
                this.controls.IsPreviousEnabled = true;
                return;
            }

            if (playlist_playing_pos > 0 && playlist_count > 0)
            {
                this.controls.IsNextEnabled = true;
                this.controls.IsPreviousEnabled = true;
                return;
            }

            Log.Warning("Failed to check playlist position");
            this.controls.IsNextEnabled = true;
            this.controls.IsPreviousEnabled = true;
            return;
        }

        private async void OnSMTCButtonPress(SystemMediaTransportControls sender,
                SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                case SystemMediaTransportControlsButton.Pause:
                    await mpv_stream.MakeAsyncRequest("cycle pause");
                    break;
                case SystemMediaTransportControlsButton.Next:
                    await mpv_stream.MakeAsyncRequest("playlist-next weak");
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    await mpv_stream.MakeAsyncRequest("playlist-prev weak");
                    break;
                default:
                    break;
            }
        }

        public async Task Start()
        {
            mpv_stream.Connect();
            Log.Information("MPV Connection started");

            mpv_stream.AddPropertyObserver("pause", this.PlayPauseObserver);
            mpv_stream.AddPropertyObserver("metadata", this.SetMetadata);
            mpv_stream.AddPropertyObserver("playlist-playing-pos", this.SetPlaylistButtons);
            mpv_stream.AddPropertyObserver("playlist", this.SetPlaylistButtons);

            controls.ButtonPressed += this.OnSMTCButtonPress;

            Log.Information("Event handling set");
        }
    }
}
