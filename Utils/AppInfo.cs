using Microsoft.Win32;
using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AhDung
{
    /// <summary>
    /// 程序集信息类
    /// </summary>
    public static class AppInfo
    {
        static readonly Assembly _assembly;
        static string _titile, _version, _company, _rightsYears, _titileAndVer, _name, _fullName, _location, _fileName, _path;

        static AppInfo()
        {
            _assembly = Assembly.GetExecutingAssembly();
        }

        /// <summary>
        /// 程序集名（不含扩展名）
        /// </summary>
        public static string Name
        {
            get
            {
                return _name ?? (_name = _assembly.GetName().Name);
            }
        }

        /// <summary>
        /// 程序集完全限定名
        /// </summary>
        public static string FullName
        {
            get
            {
                return _fullName ?? (_fullName = _assembly.FullName);
            }
        }

        /// <summary>
        /// 程序集路径
        /// </summary>
        public static string Location
        {
            get
            {
                return _location ?? (_location = _assembly.Location);
            }
        }

        /// <summary>
        /// 程序集所在目录
        /// </summary>
        public static string Path
        {
            get
            {
                return _path ?? (_path = System.IO.Path.GetDirectoryName(_assembly.Location));
            }
        }

        /// <summary>
        /// 程序集文件名（含扩展名，不含路径）
        /// </summary>
        public static string FileName
        {
            get
            {
                return _fileName ?? (_fileName = System.IO.Path.GetFileName(Location));
            }
        }

        /// <summary>
        /// 程序标题
        /// </summary>
        public static string AppTitle
        {
            get
            {
                if (_titile == null)
                {
                    AssemblyTitleAttribute attr = Attribute.GetCustomAttribute(_assembly, typeof(AssemblyTitleAttribute)) as AssemblyTitleAttribute;
                    _titile = (attr == null) ? string.Empty : attr.Title;
                }
                return _titile;
            }
        }

        /// <summary>
        /// 程序版本
        /// </summary>
        public static string AppVersion
        {
            get { return _version ?? (_version = _assembly.GetName().Version.ToString()); }
        }

        /// <summary>
        /// 公司
        /// </summary>
        public static string AppCompany
        {
            get
            {
                if (_company == null)
                {
                    AssemblyCompanyAttribute attr = Attribute.GetCustomAttribute(_assembly, typeof(AssemblyCompanyAttribute)) as AssemblyCompanyAttribute;
                    _company = (attr == null) ? string.Empty : attr.Company;
                }
                return _company;
            }
        }

        /// <summary>
        /// 版权年份。形似2015[-2015]
        /// </summary>
        public static string AppCopyrightYears
        {
            get
            {
                if (_rightsYears == null)
                {
                    AssemblyCopyrightAttribute attr = Attribute.GetCustomAttribute(_assembly, typeof(AssemblyCopyrightAttribute)) as AssemblyCopyrightAttribute;
                    _rightsYears = (attr == null) ? string.Empty
                        : Regex.Match(attr.Copyright, @"\d{4}(-\d{4})?").Value;
                }
                return _rightsYears;
            }
        }

        /// <summary>
        /// 标题+版本。形似（xxx 1.0）
        /// </summary>
        public static string TitleAndVer
        {
            get { return _titileAndVer ?? (_titileAndVer = string.Format("{0} {1}", AppTitle, AppVersion)); }
        }

        /// <summary>
        /// 设置自启状态
        /// </summary>
        /// <param name="autoStart">是否自启</param>
        /// <param name="argument">自启参数</param>
        public static void SetAutoStart(bool autoStart, string argument = null)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (autoStart)
                {
                    key.SetValue(FileName, string.Format("\"{0}\"{1}", Location, string.IsNullOrEmpty(argument) ? string.Empty : (" " + argument)));
                }
                else
                {
                    key.DeleteValue(FileName);
                }
            }
        }

        /// <summary>
        /// 获取自启状态
        /// </summary>
        /// <returns></returns>
        public static bool GetAutoStart()
        {
            object val = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run", FileName, null);
            return val != null && val.ToString().StartsWith("\"" + Location + "\"", StringComparison.OrdinalIgnoreCase);
        }
    }
}
