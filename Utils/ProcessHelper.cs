using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace AhDung
{
    /// <summary>
    /// 进程辅助类
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    public static class ProcessHelper
    {
        static readonly bool _isNt5;
        static readonly int _processAccessRights;
        static int _crrPid = -1;
        static string _crrPName;
        static string _crrPPath;
        static string _crrPPathNt;

        /// <summary>
        /// 当前进程ID
        /// </summary>
        public static int CurrentProcessId
        {
            get
            {
                return _crrPid == -1
                ? (_crrPid = NativeMethods.GetCurrentProcessId())
                : _crrPid;
            }
        }

        /// <summary>
        /// 当前进程名（含.exe）
        /// </summary>
        /// <exception cref="Win32Exception"></exception>
        public static string CurrentProcessName
        {
            get { return _crrPName ?? (_crrPName = Path.GetFileName(CurrentProcessImageFileName)); }
        }

        /// <summary>
        /// 当前进程映像文件路径
        /// </summary>
        /// <exception cref="Win32Exception"></exception>
        public static string CurrentProcessImageFileName
        {
            get { return _crrPPath ?? (_crrPPath = GetCurrentProcessImageFileName()); }
        }

        /// <summary>
        /// 当前进程映像文件路径（NT格式）
        /// <para>- 不如CurrentProcessImageFileName高效</para>
        /// </summary>
        /// <exception cref="Win32Exception"></exception>
        public static string CurrentProcessImageFileNameNt
        {
            get { return _crrPPathNt ?? (_crrPPathNt = GetCurrentProcessImageFileNameNt()); }
        }

        static ProcessHelper()
        {
            try
            {
                //需启用本进程的调试特权才能访问session0进程
                Process.EnterDebugMode();
            }
            catch { } //UAC可能会让上述操作抛异常，需吃掉

            _isNt5 = Environment.OSVersion.Version.Major == 5;
            _processAccessRights = _isNt5
                ? 0x410   //PROCESS_QUERY_INFORMATION(0x400) | PROCESS_VM_READ(0x10，读session0进程需要)
                : 0x1000; //PROCESS_QUERY_LIMITED_INFORMATION，NT6才支持该权限，好处是可以读audiodg.exe这样的进程
        }

        /// <summary>
        /// 获取当前进程映像文件路径
        /// </summary>
        /// <exception cref="Win32Exception"></exception>
        private static string GetCurrentProcessImageFileName()
        {
            //取dos路径直接用GetModuleFileName，高效
            const int MAXPATH = 260; //NT最大路径字符数，非字节数
            StringBuilder sb = new StringBuilder(MAXPATH);
            if (NativeMethods.GetModuleFileName(IntPtr.Zero, sb, MAXPATH) == 0)
            {
                throw Win32ErrorHelper.ExceptionBuilder("获取当前进程映像路径失败！");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 获取当前进程映像文件的NT路径（\Device\HarddiskVolumeX..）
        /// <para>- 不如GetCurrentProcessImageFileName高效</para>
        /// </summary>
        /// <exception cref="Win32Exception"></exception>
        private static string GetCurrentProcessImageFileNameNt()
        {
            //取NT路径采用常规方法，根据句柄取路径
            //这样比先用GetModuleFileName取得dos路径，再转nt路径来的快
            //转换用QueryDosDevice、NtQuerySymbolicLinkObject性能都不理想

            int errCode;
            IntPtr crrHandle = NativeMethods.GetCurrentProcess();//此句柄不用释放，虚拟的
            string result = GetProcessImageFileNameCore(crrHandle, true, out errCode);
            if (errCode != 0)
            {
                throw Win32ErrorHelper.ExceptionBuilder("获取当前进程映像路径失败！", errCode);
            }
            return result;
        }

        /// <summary>
        /// 根据进程ID获取映像文件路径。出错返回NULL，不抛异常
        /// <para>- 应优先使用GetProcessImageFileNameNt，性能高</para>
        /// </summary>
        public static string GetProcessImageFileName(int pid)
        {
            int errCode;
            return GetProcessImageFileNameCore(pid, false, out errCode);
        }

        /// <summary>
        /// 根据进程ID获取映像文件路径。出错返回NULL和错误码，不抛异常
        /// <para>- 应优先使用GetProcessImageFileNameNt，性能高</para>
        /// </summary>
        public static string GetProcessImageFileName(int pid, out int errorCode)
        {
            return GetProcessImageFileNameCore(pid, false, out errorCode);
        }

        /// <summary>
        /// 根据进程ID获取映像路径（NT）。出错返回NULL，不抛异常
        /// </summary>
        public static string GetProcessImageFileNameNt(int pid)
        {
            int errCode;
            return GetProcessImageFileNameCore(pid, true, out errCode);
        }

        /// <summary>
        /// 根据进程ID获取映像路径（NT）。出错返回NULL和错误码，不抛异常
        /// </summary>
        public static string GetProcessImageFileNameNt(int pid, out int errorCode)
        {
            return GetProcessImageFileNameCore(pid, true, out errorCode);
        }

        /// <summary>
        /// 根据进程ID获取映像文件路径（dos/nt格式可选，后者性能高）
        /// <para>- 出错返回null，不抛异常</para>
        /// <para>- errorCode为0表示成功</para>
        /// </summary>
        private static string GetProcessImageFileNameCore(int pid, bool inNtPath, out int errorCode)
        {
            if (pid == 0 || pid == 4)
            {
                errorCode = 5;//ERROR_ACCESS_DENIED，拒绝访问
                return null;
            }

            IntPtr p = IntPtr.Zero;
            try
            {
                //拿到句柄
                p = NativeMethods.OpenProcess(_processAccessRights, false, pid);
                if (p == IntPtr.Zero)
                {
                    errorCode = Marshal.GetLastWin32Error();
                    return null;
                }

                return GetProcessImageFileNameCore(p, inNtPath, out errorCode);
            }
            finally
            {
                if (p != IntPtr.Zero) { NativeMethods.CloseHandle(p); }
            }
        }

        /// <summary>
        /// 根据进程句柄获取映像文件路径（dos/nt格式可选，后者性能高）
        /// <para>- 出错返回null，不抛异常</para>
        /// <para>- errorCode为0表示成功</para>
        /// </summary>
        private static string GetProcessImageFileNameCore(IntPtr handle, bool inNtPath, out int errorCode)
        {
            //nt5使用GetProcessImageFileName取NT路径，用GetModuleFileNameEx取Dos路径
            //nt6用QueryFullProcessImageName一站搞掂

            StringBuilder sb = new StringBuilder(300);//nt格式可能会超过260字符
            int retVal;

            if (_isNt5)
            {
                retVal = inNtPath
                   ? NativeMethods.GetProcessImageFileName(handle, sb, sb.Capacity)
                   : NativeMethods.GetModuleFileNameEx(handle, IntPtr.Zero, sb, sb.Capacity);
            }
            else
            {
                int size = sb.Capacity;
                retVal = NativeMethods.QueryFullProcessImageName(handle, inNtPath, sb, ref size);
            }

            //上述API返回0均代表失败
            if (retVal == 0)
            {
                errorCode = Marshal.GetLastWin32Error();
                return null;
            }

            errorCode = 0;
            return sb.ToString();
        }

        /// <summary>
        /// 本程序是否存在另一个进程
        /// </summary>
        /// <param name="thisWinSta">是否仅限本窗口站范围</param>
        public static bool ExistsAnother(bool thisWinSta = false)
        {
            int pid;
            return ExistsAnother(out pid, thisWinSta);
        }

        /// <summary>
        /// 本程序是否存在另一个进程
        /// </summary>
        /// <param name="pid">找到的进程ID。找不到则返回-1</param>
        /// <param name="thisWinSta">是否仅限本窗口站范围</param>
        public static bool ExistsAnother(out int pid, bool thisWinSta = false)
        {
            foreach (var p in EnumProcesses())
            {
                //判断顺序重要，性能考虑
                if (string.Equals(CurrentProcessName, p.Name, StringComparison.OrdinalIgnoreCase)
                    && CurrentProcessId != p.Id
                    && string.Equals(CurrentProcessImageFileNameNt, GetProcessImageFileNameNt(p.Id), StringComparison.OrdinalIgnoreCase)
                    && (!thisWinSta || OnSameWinSta(p.Id)))
                {
                    pid = p.Id;
                    return true;
                }
            }
            pid = -1;
            return false;
        }

        /// <summary>
        /// 是否存在映像文件路径为imagePath的进程（Dos格式）
        /// </summary>
        public static bool ExistsByPath(string imagePath)
        {
            using (var e = EnumProcesses(imagePath).GetEnumerator())
            {
                return e.MoveNext();
            }
        }

        /// <summary>
        /// 终止进程。按映像文件路径
        /// <para>- 等候5秒，结束失败、超时或等候出错均抛异常</para>
        /// </summary>
        /// <exception cref="Win32Exception"></exception>
        public static void KillByPath(string imagePath)
        {
            List<IntPtr> handles = new List<IntPtr>();

            //遍历该路径所有进程，结束之，同时将句柄添加进列表，
            //供等候函数用，所以不能在此释放句柄
            foreach (var p in EnumProcesses(imagePath))
            {
                //0x100001 = PROCESS_TERMINATE(1) | SYNCHRONIZE(0x100000，等待需要)
                IntPtr pHandle = NativeMethods.OpenProcess(0x100001, false, p.Id);
                if (pHandle == IntPtr.Zero)
                {
                    continue;
                }

                handles.Add(pHandle);
                if (!NativeMethods.TerminateProcess(pHandle, -1))
                {
                    handles.ForEach(handle => NativeMethods.CloseHandle(handle));//先释放句柄
                    throw Win32ErrorHelper.ExceptionBuilder("结束进程失败！");
                }
            }

            //若不存在符合的进程则不用继续
            if (handles.Count == 0)
            {
                return;
            }

            //等候进程结束，或超时返回
            uint state = NativeMethods.WaitForMultipleObjects(handles.Count, handles.ToArray(), true, 5000/*5s*/);
            handles.ForEach(p => NativeMethods.CloseHandle(p));//及时释放句柄

            if (state == 0x102/*WAIT_TIMEOUT*/)
            {
                throw Win32ErrorHelper.ExceptionBuilder("结束进程失败！", 0x102);
            }
            if (state == 0xFFFFFFFF/*WAIT_FAILED*/)
            {
                throw Win32ErrorHelper.ExceptionBuilder("结束进程失败！");
            }
        }

        /// <summary>
        /// 遍历指定映像文件路径的进程
        /// </summary>
        /// <param name="imagePath">映像路径</param>
        /// <param name="isNtPath">是否NT路径格式（true快）</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="Win32Exception"></exception>
        public static IEnumerable<ProcessInfo> EnumProcesses(string imagePath, bool isNtPath = false)
        {
            if (imagePath == null)
            {
                throw new ArgumentNullException();
            }

            string pName = Path.GetFileName(imagePath);
            GetStringByIntDelegate getImgFile = isNtPath
                ? new GetStringByIntDelegate(GetProcessImageFileNameNt)
                : GetProcessImageFileName;

            foreach (var p in EnumProcesses())
            {
                if (string.Equals(pName, p.Name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(imagePath, getImgFile(p.Id), StringComparison.OrdinalIgnoreCase))
                {
                    yield return p;
                }
            }
        }

        private delegate string GetStringByIntDelegate(int pid);

        /// <summary>
        /// 遍历进程
        /// </summary>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="Win32Exception"></exception>
        public static IEnumerable<ProcessInfo> EnumProcesses()
        {
            //遍历进程NtQuerySystemInformation比EnumProcesses快

            IntPtr pt = IntPtr.Zero;  //存放所有进程信息
            try
            {
                uint bytes = 0x40000; //初始来个256K
                uint result;
                do
                {
                    pt = Marshal.AllocHGlobal((int)bytes);
                    result = NativeMethods.NtQuerySystemInformation(5/*SystemProcessInformation*/, pt, bytes, out result/*无谓多弄个变量*/);
                    if (result == 0xC0000004)//STATUS_INFO_LENGTH_MISMATCH，表示大小不够
                    {
                        Marshal.FreeHGlobal(pt); //释放先前分配的
                        bytes += 0x10000;        //老板再来64K
                    }
                } while (result == 0xC0000004);

                if (result != 0)//若为其它错误
                {
                    throw Win32ErrorHelper.ExceptionBuilder("遍历进程出错！", (int)result);
                }

                //从pt中逐一拿到进程信息
                NativeMethods.SystemProcessInformation pInfo;
                IntPtr next = pt;
                do
                {
                    pInfo = new NativeMethods.SystemProcessInformation();
                    Marshal.PtrToStructure(next, pInfo);

                    var id = (int)pInfo.UniqueProcessId;
                    var name = id == 0 ? "Idle" : Marshal.PtrToStringUni(pInfo.NamePtr);

                    yield return new ProcessInfo(id, name);
                }
                while (pInfo.NextEntryOffset != 0
                    && (next = new IntPtr(((long)next) + pInfo.NextEntryOffset)) != IntPtr.Zero);//伪条件，为了赋值
            }
            finally
            {
                Marshal.FreeHGlobal(pt);
            }
        }

        /// <summary>
        /// 检测进程是否与本进程在同一个窗口站
        /// </summary>
        /// <exception cref="Win32Exception" />
        public static bool OnSameWinSta(int pid)
        {
            //取得本进程所在窗口站
            var ws = NativeMethods.GetProcessWindowStation();
            if (ws == IntPtr.Zero)
            {
                throw new Win32Exception();
            }

            bool result = false;

            //遍历本窗口站下的所有桌面
            NativeMethods.EnumDesktops(ws, (name, i) =>
            {
                var desk = NativeMethods.OpenDesktop(name, 0, false, NativeMethods.DESK_GENERIC_READ);
                if (desk == IntPtr.Zero)
                {
                    return true;
                }

                //遍历每个桌面中的所有窗口，并将窗口所在进程与目标进程比较
                //对得上就返回true并结束后续遍历
                NativeMethods.EnumDesktopWindows(desk, (window, prm) =>
                {
                    int pid2;
                    NativeMethods.GetWindowThreadProcessId(window, out pid2);
                    if (pid == pid2)
                    {
                        result = true;
                        return false;//若已找到，返回false结束遍历
                    }
                    return true;
                }, 0);

                NativeMethods.CloseDesktop(desk);

                return !result;//根据result决定是否继续遍历
            }, 0);

            return result;
        }

        /// <summary>
        /// Win32 API
        /// </summary>
        private static class NativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto), PreserveSig]
            public static extern int GetModuleFileName(IntPtr hModule, [Out]StringBuilder lpFilename, int nSize);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern int GetCurrentProcessId();

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr GetCurrentProcess();

            [DllImport("psapi.dll", SetLastError = true)]
            public static extern int GetProcessImageFileName(IntPtr hProcess, [Out] StringBuilder lpImageFileName, int nSize);

            [DllImport("ntdll.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern uint NtQuerySystemInformation(int query, IntPtr dataPtr, uint size, out uint returnedSize);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool TerminateProcess(IntPtr hProcess, int uExitCode);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CloseHandle(IntPtr hObject);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern int QueryFullProcessImageName(IntPtr hProcess, bool dwFlags, [Out] StringBuilder lpImageFileName, ref int nSize);

            [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern int GetModuleFileNameEx(IntPtr processHandle, IntPtr moduleHandle, [Out]StringBuilder baseName, int size);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern uint WaitForMultipleObjects(int nCount, IntPtr[] handles, bool bWaitAll, int dwMilliseconds);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern int FormatMessage(int dwFlags, IntPtr lpSource, int dwMessageId, int dwLanguageId, StringBuilder lpBuffer, int nSize, IntPtr va_list_arguments);

            [StructLayout(LayoutKind.Sequential)]
            public class SystemProcessInformation
            {
                // ReSharper disable UnassignedField.Compiler
                // ReSharper disable UnusedField.Compiler
                internal int NextEntryOffset;
                internal uint NumberOfThreads;
                private long SpareLi1;
                private long SpareLi2;
                private long SpareLi3;
                private long CreateTime;
                private long UserTime;
                private long KernelTime;
                internal ushort NameLength;
                internal ushort MaximumNameLength;
                internal IntPtr NamePtr;
                internal int BasePriority;
                internal IntPtr UniqueProcessId;//不可改为uint，IntPtr在x86/x64大小不同
                internal IntPtr InheritedFromUniqueProcessId;
                internal uint HandleCount;
                internal uint SessionId;
                internal IntPtr PageDirectoryBase;
                internal IntPtr PeakVirtualSize;
                internal IntPtr VirtualSize;
                internal uint PageFaultCount;
                internal IntPtr PeakWorkingSetSize;
                internal IntPtr WorkingSetSize;
                internal IntPtr QuotaPeakPagedPoolUsage;
                internal IntPtr QuotaPagedPoolUsage;
                internal IntPtr QuotaPeakNonPagedPoolUsage;
                internal IntPtr QuotaNonPagedPoolUsage;
                internal IntPtr PagefileUsage;
                internal IntPtr PeakPagefileUsage;
                internal IntPtr PrivatePageCount;
                private long ReadOperationCount;
                private long WriteOperationCount;
                private long OtherOperationCount;
                private long ReadTransferCount;
                private long WriteTransferCount;
                private long OtherTransferCount;
                // ReSharper restore UnusedField.Compiler
                // ReSharper restore UnassignedField.Compiler
            }

            public const uint DESK_GENERIC_READ = 0x20000 //READ_CONTROL(STANDARD_RIGHTS_READ)
                                                | 0x40    //DESKTOP_ENUMERATE
                                                | 0x1     //DESKTOP_READOBJECTS
                                                ;

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr GetProcessWindowStation();

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool EnumDesktops(IntPtr hwinsta, EnumWindowStationProc lpEnumFunc, int lParam);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumWindowsProc lpfn, int lParam);

            [DllImport("user32.dll")]
            public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool CloseDesktop(IntPtr hDesktop);
        }

        /// <summary>
        /// Win32 异常辅助类
        /// </summary>
        private static class Win32ErrorHelper
        {
            /// <summary>
            /// 获取系统错误代码对应消息
            /// </summary>
            /// <remarks>从Win32Exception中扒的</remarks>
            private static string GetErrorMessage(int error)
            {
                StringBuilder lpBuffer = new StringBuilder(0x100);
                if (NativeMethods.FormatMessage(0x3200, IntPtr.Zero, error, 0, lpBuffer, lpBuffer.Capacity + 1, IntPtr.Zero) == 0)
                {
                    return ("Unknown error (0x" + Convert.ToString(error, 0x10) + ")");
                }
                int length = lpBuffer.Length;
                while (length > 0)
                {
                    char ch = lpBuffer[length - 1];
                    if ((ch > ' ') && (ch != '.'))
                    {
                        break;
                    }
                    length--;
                }
                return lpBuffer.ToString(0, length);
            }

            /// <summary>
            /// Win32 异常构造。用户定义消息+系统错误消息+错误代码
            /// </summary>
            /// <param name="message">用户定义消息，建议带标点</param>
            /// <param name="error">错误代码</param>
            public static Win32Exception ExceptionBuilder(string message = null, int? error = null)
            {
                int code = error ?? Marshal.GetLastWin32Error();

                if (string.IsNullOrEmpty(message))
                {
                    return new Win32Exception(code);
                }

                return new Win32Exception(code, message + GetErrorMessage(code) + code);
            }
        }
    }

    /// <summary>
    /// 用于遍历窗口站或桌面的委托
    /// </summary>
    /// <param name="lpszWindowStation">窗口站名或桌面名</param>
    /// <param name="lParam">由遍历函数传入的自定义参数</param>
    /// <returns>返回true为继续遍历，false终止遍历</returns>
    public delegate bool EnumWindowStationProc(string lpszWindowStation, int lParam);

    /// <summary>
    /// 用于遍历桌面窗口的委托
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="lParam">由遍历函数传入的自定义参数</param>
    /// <returns>返回true为继续遍历，false终止遍历</returns>
    public delegate bool EnumWindowsProc(IntPtr hwnd, int lParam);

    /// <summary>
    /// 承载单个进程信息
    /// </summary>
    public class ProcessInfo
    {
        /// <summary>
        /// 进程ID
        /// </summary>
        public readonly int Id;

        /// <summary>
        /// 进程名（含.exe）
        /// </summary>
        public readonly string Name;

        internal ProcessInfo(int id, string name)
        {
            Id = id;
            Name = name;
        }

        /// <summary>
        /// 输出Pid:Name
        /// </summary>
        public override string ToString()
        {
            return string.Format("{0}:{1}", Id, Name);
        }
    }
}
