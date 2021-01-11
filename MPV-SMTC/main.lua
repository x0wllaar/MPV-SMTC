--OPTIONS
LOG_LEVEL = 2
-- 0 = Verbose
-- 1 = Debug
-- 2 = Information
-- 3 = Warning
-- 4 = Error
-- 5 = Fatal

PIPE_ADDR_TEMPLATE = "mpvsocket_" --Pipe name prefix
PIPE_ID_LENGTH = 15 --Length of the random ID of the named pipe
REL_EXE_LOCATION = "MPV-SMTC.exe" --Name of the exe file in the script directory
PIPE_ADDR_PATH = [[\\.\pipe\]] --Path to the pipe namespace (you probably should't touch this)

local charset = {}  do -- [0-9a-zA-Z]
    for c = 48, 57  do table.insert(charset, string.char(c)) end
    for c = 65, 90  do table.insert(charset, string.char(c)) end
    for c = 97, 122 do table.insert(charset, string.char(c)) end
end

local function randomString(length)
    if not length or length <= 0 then return '' end
    math.randomseed(os.clock()^5)
    return randomString(length - 1) .. charset[math.random(1, #charset)]
end

pipe_name = PIPE_ADDR_TEMPLATE .. randomString(PIPE_ID_LENGTH)
pipe_addr = PIPE_ADDR_PATH .. pipe_name
mp.msg.info("Setting pipe to " .. pipe_addr)
mp.set_property("input-ipc-server", pipe_addr)

script_dir = mp.get_script_directory()
exe_path = script_dir .. "/" .. REL_EXE_LOCATION
mp.msg.info("Starting MPV-SMTC executable")
mp.command_native({
    name = "subprocess",
    playback_only = false,
    capture_stdout = false,
    detach = true,
    args = {
        exe_path, 
        "--pipename=" .. pipe_name, 
        "--loglevel=" .. LOG_LEVEL},
})