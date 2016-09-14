using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace AhDung
{
    /// <summary>
    /// 单例程序辅助类
    /// <para>- 需将本类源码放置在主程序集中使用，不可封装成dll再调用，那可能会导致判断不准</para>
    /// </summary>
    public static class AppSingleton
    {
        //需用类成员保持对互斥体的引用
        //阻止其在程序运行期间被回收
        // ReSharper disable once NotAccessedField.Local
        static Mutex _mutex;

        private static string _mutexNamePrefix;

        /// <summary>
        /// 互斥体名前缀
        /// </summary>
        private static string MutexNamePrefix
        {
            get
            {
                if (_mutexNamePrefix == null)
                {
                    //互斥名采用【程序集名+程序集GUID】
                    Assembly asm = Assembly.GetExecutingAssembly();
                    string name = asm.GetName().Name;
                    string guid = ((GuidAttribute)Attribute.GetCustomAttribute(asm, typeof(GuidAttribute))).Value;
                    _mutexNamePrefix = name + guid;
                }
                return _mutexNamePrefix;
            }
        }

        /// <summary>
        /// 确保单例。若存在则执行action，然后退出自身
        /// <para>- 仅限当前进程处于交互模式时才会检测</para>
        /// <para>- 忽略本窗口站以外的进程</para>
        /// </summary>
        /// <param name="pathSensitive">是否对路径敏感</param>
        /// <param name="action">已存在时要执行的动作，参数为另一实例的进程ID</param>
        public static void Ensure(bool pathSensitive = false, Action<uint> action = null)
        {
            if (!Environment.UserInteractive)
            {
                return;
            }

            bool exist = false;
            uint pid = 0;

            //路经相关时：遍历所有该路径的进程（自身除外）
            if (pathSensitive)
            {
                foreach (var p in ProcessHelper.EnumProcesses(ProcessHelper.CurrentProcessImageFileName))
                {
                    if (p.Id == ProcessHelper.CurrentProcessId
                        || !ProcessHelper.IsProcessOnWinSta(p.Id))
                    {
                        continue;
                    }

                    exist = true;
                    pid = p.Id;
                    break;
                }
            }
            else//全局时：遍历所有进程，并用固定前缀+pid得到需检测的互斥体名
            {
                foreach (var p in ProcessHelper.EnumProcesses())
                {
                    if (p.Id == ProcessHelper.CurrentProcessId
                        || !string.Equals(p.Name, ProcessHelper.CurrentProcessName)//如果要防止改名多开，需注释该行
                        || !ProcessHelper.IsProcessOnWinSta(p.Id))
                    {
                        continue;
                    }

                    if (ExistMutex(MakeMutexName(p.Id)))
                    {
                        exist = true;
                        pid = p.Id;
                        break;
                    }
                }

                if (!exist)//若不存在指定互斥体，创建一个
                {
                    _mutex = new Mutex(true, MakeMutexName(ProcessHelper.CurrentProcessId));
                }
            }

            if (exist)
            {
                if (action != null)
                {
                    action(pid);
                }
                Environment.Exit(Environment.ExitCode);
            }
        }

        /// <summary>
        /// 根据进程ID生成互斥体名
        /// </summary>
        private static string MakeMutexName(uint pid)
        {
            return MutexNamePrefix + pid;
        }

        /// <summary>
        /// 是否存在指定名称的互斥体
        /// </summary>
        private static bool ExistMutex(string name)
        {
            Mutex mutex = null;
            try
            {
                mutex = Mutex.OpenExisting(name);
                return true;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }
            finally
            {
                if (mutex != null) { mutex.Close(); }
            }
        }

        //WMI方式
        //private static bool ExistProcessWMI()
        //{
        //    Assembly asm = Assembly.GetExecutingAssembly();
        //    string wql = string.Format(@"select Handle from Win32_Process where ExecutablePath = '{0}'", asm.Location.Replace("\\", "\\\\"));
        //    using (var q = new ManagementObjectSearcher(wql))
        //    {
        //        return q.Get().Count > 1;
        //    }
        //}
    }
}
