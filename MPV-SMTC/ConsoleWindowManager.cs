﻿using System;
using System.Runtime.InteropServices;

namespace MPVSMTC
{
    class ConsoleWindowManager
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        public static extern bool AttachConsole(int dwProcessId);
        public const int ATTACH_PARENT_PROCESS = -1;

        public enum ShowWindowCommands {
            SW_FORCEMINIMIZE = 11,
            SW_HIDE = 0,
            SW_MAXIMIZE = 3,
            SW_MINIMIZE = 6,
            SW_RESTORE = 9,
            SW_SHOW = 5,
            SW_SHOWDEFAULT = 10,
            SW_SHOWMAXIMIZED = 3,
            SW_SHOWMINIMIZED = 2,
            SW_SHOWMINNOACTIVE = 7,
            SW_SHOWNA = 8,
            SW_SHOWNOACTIVATE = 4,
            SW_SHOWNORMAL = 1
        }

        public static bool ShowWindowM(ShowWindowCommands cmd)
        {
            var handle = GetConsoleWindow();
            return ShowWindow(handle, (int)cmd);
        }

        public static bool HideWindow()
        {
            return ShowWindowM(ShowWindowCommands.SW_HIDE);
        }

        public static bool UnhideWindow()
        {
            return ShowWindowM(ShowWindowCommands.SW_SHOW);
        }

        public static bool ConnectConsole()
        {
            if (!AttachConsole(ATTACH_PARENT_PROCESS))
            {
                return AllocConsole();
            }
            else
            {
                return true;
            }
        }
    }
}
