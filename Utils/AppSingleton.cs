using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace AhDung
{
    /// <summary>
    /// 单例程序辅助类
    /// </summary>
    /// <remarks>
    /// 本方案原理：
    /// - 根据条件，用不同方式找已经存在的实例
    ///   - 路径攸关时，找进程（忽略本窗口站以外的进程，以避开承载服务的进程）
    ///   - 否则找邮槽
    /// - 找到后可选是否显示对方，然后退出自身
    /// - 没找到的话，按需创建邮槽并接收消息，当收到显示代号时，触发ShowCodeReceived事件
    /// - 程序注册上述事件，如何显示自己负责实现
    /// </remarks>
    public static class AppSingleton
    {
        /// <summary>
        /// 约定的显示代号
        /// </summary>
        const short ShowCode = -31753;//0x83F7

        /// <summary>
        /// 同步上下文
        /// </summary>
        static SynchronizationContext _syncContext;

        static EventHandler _showCodeReceived;
        /// <summary>
        /// 接收到显示代号后。注意如果在窗体启动前注册，事件可能运行在后台线程
        /// </summary>
        public static event EventHandler ShowCodeReceived
        {
            add
            {
                //之所以放这里取同步上下文，是因为通常是在窗体内注册该事件
                //意味着执行到这里时，窗体已经启动
                //此时访问SynchronizationContext.Current才能拿到UI上下文
                //而在窗体启动前是拿不到的，比如Main方法中
                //拿UI上下文的目的是在邮槽读取线程中，将事件PO到UI线程执行
                if (_syncContext == null)
                {
                    _syncContext = SynchronizationContext.Current;
                }
                _showCodeReceived += value;
            }
            remove
            {
                // ReSharper disable once DelegateSubtraction
                _showCodeReceived -= value;
            }
        }

        static string _slotName;
        /// <summary>
        /// 邮槽名（程序集名+程序集GUID+ForShow）
        /// </summary>
        private static string SlotName
        {
            get
            {
                if (_slotName == null)
                {
                    Assembly asm = Assembly.GetEntryAssembly();
                    string name = asm.GetName().Name;
                    string guid = ((GuidAttribute)Attribute.GetCustomAttribute(asm, typeof(GuidAttribute))).Value;
                    _slotName = name + guid + "ForSingleton";
                }
                return _slotName;
            }
        }

        /// <summary>
        /// 带进程ID的邮槽名。pid为-1时返回原名
        /// </summary>
        private static string SlotNameAppendPid(int pid)
        {
            return pid == -1 ? SlotName : (SlotName + pid);
        }

        /// <summary>
        /// 确保单例。若存在则退出自身
        /// <para>- 仅限当前进程处于交互模式时才会检测</para>
        /// <para>- 忽略本窗口站以外的进程</para>
        /// </summary>
        /// <param name="sysWide">是否在系统范围检测。否则仅检测同路径</param>
        /// <param name="showIt">是否让已存实例显示。如果为true，记得注册ShowCodeReceived事件</param>
        public static void Ensure(bool sysWide = false, bool showIt = false)
        {
            if (!Environment.UserInteractive)
            {
                return;
            }

            int pid = -1;

            bool exists = sysWide
                ? MailslotUtil.Exists(SlotName)               //找邮槽
                : ProcessHelper.ExistsAnother(out pid, true); //找进程

            if (exists)
            {
                if (showIt)
                {
                    SendShowCode(SlotNameAppendPid(pid));
                }
                Environment.Exit(-1);
            }

            //仅当需要时才开邮槽
            if (sysWide    //为了让人找到
                || showIt) //为了接收显示代号
            {
                MailslotUtil.Create(SlotNameAppendPid(sysWide ? -1 : ProcessHelper.CurrentProcessId));
                BeginReceive();
            }
        }

        /// <summary>
        /// 向已存实例发送显示代号
        /// </summary>
        private static void SendShowCode(string slotName)
        {
            try
            {
                MailslotUtil.Write(slotName, ShowCode);
            }
            catch (Win32Exception ex)
            {
                //忽略邮槽不存在异常。因为在发送时对方也许已经关闭
                if (ex.NativeErrorCode != 2) { throw; }
            }
        }

        /// <summary>
        /// 开始接收邮槽消息
        /// </summary>
        private static void BeginReceive()
        {
            new Thread(none =>
            {
                do
                {
                    if (MailslotUtil.ReadInt16() == ShowCode)
                    {
                        if (_syncContext == null)
                        {
                            RaiseShowCodeReceived();
                        }
                        else
                        {
                            _syncContext.Post(arg => RaiseShowCodeReceived(), null);
                        }
                    }
                } while (true);

                // ReSharper disable once FunctionNeverReturns
            }) { IsBackground = true }.Start();
        }

        /// <summary>
        /// 触发ShowCodeReceived事件
        /// </summary>
        private static void RaiseShowCodeReceived()
        {
            var handler = _showCodeReceived;
            if (handler != null)
            {
                handler(null, null);
            }
        }
    }
}
