using Stylet;
using System;
using System.IO;
using System.Reflection;
using PlaythingADB.Properties;
using System.IO.Compression;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PlaythingADB.Models;
using System.Diagnostics;
using HandyControl.Controls;
using HandyControl.Data;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq;

namespace PlaythingADB.ViewModels
{
    public class RootViewModel : Screen
    {
        public string Title { get; set; } = "Play something with ADB...";
        public string AdbPath { get; set; }
        public string AdbVersion { get; set; } = "ADB " + Resource.IdentifyErrors;
        public bool AdbEnable { get; set; } = true;
        public bool IsLoading { get; set; } = true;
        public ObservableCollection<AppOverlay> AppOverlays { get; set; } = new();

        public RootViewModel()
        {
            Task.Run(() =>
            {
                System.Windows.MessageBoxResult r = MessageBox.Show(
                    new MessageBoxInfo()
                    {
                        Caption = Resource.Warning,
                        Message = "本程序未经过大量仔细严格的测试，因使用本程序造成的手机异常，本程序及其开发人员概不负责！"
                                    + Environment.NewLine
                                    + "注意：本程序的开发人员只负责编码与跑路！其余概不负责！！！"
                    });

                if(!r.ToString().Contains("OK", StringComparison.OrdinalIgnoreCase))
                {
                    Environment.Exit(0);
                }

                AdbPath = AppDomain.CurrentDomain.BaseDirectory;
                AdbPath = Path.Combine(AdbPath, "ADB Shell");

                #region ADB Shell Check

                if (!Directory.Exists(AdbPath))
                {
                    Directory.CreateDirectory(AdbPath);
                }

                if (!CheckADBShell(AdbPath))
                {
                    var zipPath = Path.Combine(AdbPath, "MiniADB.zip");
                    File.WriteAllBytes(zipPath, Resource.MiniADB);
                    ZipFile.ExtractToDirectory(zipPath, AdbPath, true);
                    File.Delete(zipPath);
                }

                #endregion

                #region ADB Version Check

                AdbVersion = GetCommandResultLine("adb version");
                if (string.IsNullOrWhiteSpace(AdbVersion)
                    || !AdbVersion.Contains("Android Debug Bridge version", StringComparison.OrdinalIgnoreCase))
                {
                    AdbVersion = "ADB " + Resource.IdentifyErrors;
                    MessageBox.Show(
                        new MessageBoxInfo()
                        {
                            Caption = Resource.Error,
                            Message = AdbVersion
                        });

                    AdbEnable = false;
                    Loading(false);
                    return;
                }

                AdbVersion = AdbVersion.Trim('\r', '\n');
                Loading(false);

                #endregion
            });
        }

        public void GetOverlayList()
        {
            Loading(true);
            Task.Run(() =>
            {
                if (!CheckPhoneConnect())
                {
                    Loading(false);
                    return;
                }

                string overlayString = GetCommandResultLine("adb shell cmd overlay list");

                Execute.OnUIThreadAsync(() => AppOverlays.Clear());

                foreach (string overlay in Regex.Split(overlayString, @"\r|\n").Where(d => !string.IsNullOrWhiteSpace(d)).ToList())
                {
                    if (Regex.IsMatch(overlay.Trim(), @"^(\[|\-)"))
                    {
                        // overlay子项
                        string groupName = overlay[4..overlay.LastIndexOf('.')];
                        if (overlay.Contains('_', StringComparison.OrdinalIgnoreCase))
                        {
                            groupName = overlay[4..overlay.LastIndexOf('_')];
                            groupName = groupName[..groupName.LastIndexOf('.')];
                        }

                        Execute.OnUIThreadAsync(() =>
                        {
                            int overlayIndex = AppOverlays[^1].OverlayGroups.FindIndex(g => g.GroupName == groupName);
                            if (overlayIndex > -1)
                            {
                                AppOverlays[^1].OverlayGroups[overlayIndex].Overlays.Add(new OverlayModel()
                                {
                                    Name = overlay[4..],
                                    IsChecked = overlay[1].ToString().Contains('x', StringComparison.OrdinalIgnoreCase),
                                    IsEnable = !overlay[1].ToString().Contains('-', StringComparison.OrdinalIgnoreCase)
                                });
                            }
                            else
                            {
                                OverlayGroup overlayGroup = new()
                                {
                                    GroupName = groupName
                                };
                                overlayGroup.Overlays.Add(new OverlayModel()
                                {
                                    Name = overlay[4..],
                                    IsChecked = overlay[1].ToString().Contains('x', StringComparison.OrdinalIgnoreCase),
                                    IsEnable = !overlay[1].ToString().Contains('-', StringComparison.OrdinalIgnoreCase)
                                });
                                AppOverlays[^1].OverlayGroups.Add(overlayGroup);
                            }
                        });
                    }
                    else
                    {
                        Execute.OnUIThreadAsync(() => {
                            // app项
                            AppOverlays.Add(new AppOverlay() { AppName = overlay });
                        });
                    }
                }

                Loading(false);
            });
        }

        public void CheckedOverlay(string overlay)
        {
            Loading(true);
            Task.Run(() =>
            {
                GetCommandResultLine("adb shell cmd overlay enable " + overlay);

                GetOverlayList();
            });
        }

        public void UnCheckedOverlay(string overlay)
        {
            Loading(true);

            Task.Run(() =>
            {
                GetCommandResultLine("adb shell cmd overlay disable " + overlay);

                GetOverlayList();
            });
        }

        private static bool CheckADBShell(string adbPath)
        {
            List<string> adbFiles = new()
            {
                "adb.exe",
                "AdbWinApi.dll",
                "AdbWinUsbApi.dll",
                "cmd-here.exe",
                "fastboot.exe"
            };

            foreach (string adbFile in adbFiles)
            {
                if (!File.Exists(Path.Combine(adbPath, adbFile)))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CheckPhoneConnect()
        {
            string deviceString = GetCommandResultLine("adb devices");

            List<string> devices = Regex.Split(deviceString, @"\r|\n").Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

            if (devices.Count > 2)
            {
                MessageBox.Show(
                    new MessageBoxInfo()
                    {
                        Caption = Resource.Error,
                        Message = Resource.OnlySupportOne
                    });

                return false;
            }

            if (!devices[0].Contains("List of devices attached", StringComparison.OrdinalIgnoreCase)
                || devices.Count < 2)
            {
                MessageBox.Show(
                    new MessageBoxInfo()
                    {
                        Caption = Resource.Error,
                        Message = Resource.NoAndroidPhoneConnect
                    });

                return false;
            }

            var deviceStates = Regex.Split(devices[1], @"\s|\t").Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
            if (deviceStates.Count == 2)
            {
                if (!deviceStates[1].Contains("device", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        new MessageBoxInfo()
                        {
                            Caption = Resource.Error,
                            Message = Resource.CheckUSBDebug
                        });
                    return false;
                }
            }
            else
            {
                MessageBox.Show(
                    new MessageBoxInfo()
                    {
                        Caption = Resource.Error,
                        Message = Resource.UnknownADBLinkState
                    });
                return false;
            }

            return true;
        }

        private string GetCommandResultLine(string command)
        {
            Process process = new ()
            {
                StartInfo = new ProcessStartInfo(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", command)
                {
                    WorkingDirectory = AdbPath,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();

            StreamReader reader = process.StandardOutput;

            return reader.ReadToEnd();
        }

        public string TextBlock4CommandLine { get; set; }
        //public string TextBox4CommandInput { get; set; }
        private Process PowershellProcess { get; set; }
        private StreamWriter StreamWriter { get; set; }

        public void RunCommand(string script)
        {
            if (PowershellProcess is null || StreamWriter is null)
            {
                PowershellProcess = new()
                {
                    StartInfo = new ProcessStartInfo(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe")
                    {
                        WorkingDirectory = AdbPath,
                        UseShellExecute = false,    //是否使用操作系统shell启动
                        RedirectStandardInput = true,//接受来自调用程序的输入信息
                        RedirectStandardOutput = true,//由调用程序获取输出信息
                        RedirectStandardError = true,//重定向标准错误输出
                        CreateNoWindow = true,//不显示程序窗口
                    }
                };
                PowershellProcess.OutputDataReceived += PowershellProcess_OutputDataReceived;
                PowershellProcess.ErrorDataReceived += PowershellProcess_ErrorDataReceived;
                PowershellProcess.Start();
                PowershellProcess.BeginOutputReadLine();
                PowershellProcess.BeginErrorReadLine();
                StreamWriter = PowershellProcess.StandardInput;
            }

            if (string.IsNullOrWhiteSpace(script))
            {
                script = Environment.NewLine;
            }

            //TextBlock4CommandLine += nameof(PlaythingADB) + ">" + TextBox4CommandInput;

            StreamWriter.WriteLine(script);
        }

        private void PowershellProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                TextBlock4CommandLine += Environment.NewLine + e.Data + Environment.NewLine;
            }
        }

        private void PowershellProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                TextBlock4CommandLine += Environment.NewLine + e.Data + Environment.NewLine;
            }
        }

        private void Loading(bool isLoading)
        {
            Execute.OnUIThreadAsync(() => IsLoading = isLoading);
        }
    }
}
