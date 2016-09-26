using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace AhDung
{
    /// <summary>
    /// 单例程序辅助类
    /// <para>- 需将本类源码放置在主程序集中使用，不可封装成dll再调用，那样会导致判断不准</para>
    /// </summary>
    /// <remarks>
    /// 本方案原理：
    /// - 遍历本窗口站进程（目的是避开承载服务的进程），得到同名、同路径（若pathSensitive=true）进程
    /// - 若找到，则向它发送约定好的显示代号，然后退出自身
    /// - 否则创建邮槽并接收消息，当收到显示代号时，触发特定事件
    /// - 程序主窗体注册上述事件，显示自身
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
        static SynchronizationContext SyncContext;

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
                if (SyncContext == null)
                {
                    SyncContext = SynchronizationContext.Current;
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
                    Assembly asm = Assembly.GetExecutingAssembly();
                    string name = asm.GetName().Name;
                    string guid = ((GuidAttribute)Attribute.GetCustomAttribute(asm, typeof(GuidAttribute))).Value;
                    _slotName = name + guid + "ForShow";
                }
                return _slotName;
            }
        }

        /// <summary>
        /// 带进程ID的邮槽名。pid为0时返回原名
        /// </summary>
        private static string SlotNameAppendPid(uint pid)
        {
            return pid == 0 ? SlotName : (SlotName + pid);
        }

        /// <summary>
        /// 确保单例。若存在则执行action，然后退出自身
        /// <para>- 仅限当前进程处于交互模式时才会检测</para>
        /// <para>- 忽略本窗口站以外的进程</para>
        /// </summary>
        /// <param name="pathSensitive">是否对路径敏感</param>
        public static void Ensure(bool pathSensitive = false)
        {
            if (!Environment.UserInteractive)
            {
                return;
            }

            bool exists = false;
            uint pid = 0;

            if (pathSensitive)
            {
                foreach (var p in ProcessHelper.EnumProcesses(ProcessHelper.CurrentProcessImageFileNameNt, true))
                {
                    if (p.Id == ProcessHelper.CurrentProcessId
                        || !ProcessHelper.OnSameWinSta(p.Id))
                    {
                        continue;
                    }
                    exists = true;
                    pid = p.Id;
                    break;
                }
            }
            else if (MailslotUtil.Exists(SlotName))
            {
                exists = true;
            }

            if (exists)
            {
                SendShowCode(SlotNameAppendPid(pid));
                Environment.Exit(-1);//Env.Exit后面代码的不会执行
            }

            MailslotUtil.Create(SlotNameAppendPid(pathSensitive ? ProcessHelper.CurrentProcessId : 0));
            BeginReceive();
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
                        if (SyncContext == null)
                        {
                            RaiseShowCodeReceived();
                        }
                        else
                        {
                            SyncContext.Post(arg => RaiseShowCodeReceived(), null);
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
