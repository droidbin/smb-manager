using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace SmbManagerUninstall
{
    internal static class UninstallProgram
    {
        private const string ProductName = "SMB Manager";
        private const string ShortcutName = "SMB Manager.lnk";
        private const string UninstallShortcutName = "SMB Manager Uninstall.lnk";

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UninstallForm());
        }

        private sealed class UninstallForm : Form
        {
            private readonly CheckBox _removeUserDataBox;
            private readonly TextBox _logBox;
            private readonly Button _uninstallButton;
            private readonly string _runDir = AppDomain.CurrentDomain.BaseDirectory;
            private readonly string _installDir = ResolveInstallDirectory();

            public UninstallForm()
            {
                Text = ProductName + " Uninstall";
                StartPosition = FormStartPosition.CenterScreen;
                MinimumSize = new Size(520, 360);
                Size = new Size(600, 420);
                Font = new Font("Noto Sans CJK KR", 9f, FontStyle.Regular, GraphicsUnit.Point);
                BackColor = Color.FromArgb(243, 243, 243);

                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(16),
                    ColumnCount = 1,
                    RowCount = 4
                };
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
                Controls.Add(root);

                root.Controls.Add(new Label
                {
                    Text = ProductName + " 제거",
                    Dock = DockStyle.Fill,
                    Font = new Font(Font, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft
                }, 0, 0);

                var options = new GroupBox { Text = "제거 옵션", Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Color.White };
                _removeUserDataBox = new CheckBox
                {
                    Text = "사용자 설정, 진단 로그, 저장된 비밀번호도 삭제",
                    AutoSize = true,
                    Dock = DockStyle.Top
                };
                options.Controls.Add(_removeUserDataBox);
                root.Controls.Add(options, 0, 1);

                _logBox = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    BackColor = Color.FromArgb(17, 24, 39),
                    ForeColor = Color.FromArgb(230, 230, 230)
                };
                root.Controls.Add(_logBox, 0, 2);

                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
                _uninstallButton = new Button { Text = "제거", Width = 90, Height = 32 };
                _uninstallButton.Click += delegate { Uninstall(); };
                var closeButton = new Button { Text = "닫기", Width = 90, Height = 32 };
                closeButton.Click += delegate { Close(); };
                buttons.Controls.Add(_uninstallButton);
                buttons.Controls.Add(closeButton);
                root.Controls.Add(buttons, 0, 3);

                Log("실행 경로: " + _runDir);
                Log("제거 대상 설치 경로: " + _installDir);
            }

            private void Uninstall()
            {
                if (MessageBox.Show("SMB Manager를 제거할까요?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }

                try
                {
                    _uninstallButton.Enabled = false;
                    RemoveShortcuts();
                    RemoveUpdateInstallRoot();

                    if (_removeUserDataBox.Checked)
                    {
                        RemoveUserData();
                    }

                    ScheduleInstallDirectoryRemoval();
                    MessageBox.Show("제거 작업을 예약했습니다.\r\n창을 닫으면 설치 폴더가 삭제됩니다.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }
                catch (Exception error)
                {
                    Log("제거 실패: " + error.Message);
                    MessageBox.Show("제거 중 오류가 발생했습니다.\r\n" + error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _uninstallButton.Enabled = true;
                }
            }

            private void RemoveShortcuts()
            {
                DeleteFileIfExists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName));

                var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", ProductName);
                DeleteFileIfExists(Path.Combine(startMenu, ShortcutName));
                DeleteFileIfExists(Path.Combine(startMenu, UninstallShortcutName));
                TryDeleteDirectory(startMenu);
                Log("바로가기 정리 완료");
            }

            private void RemoveUpdateInstallRoot()
            {
                var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(baseDir))
                {
                    return;
                }

                TryDeleteDirectory(Path.Combine(baseDir, ProductName, "Installed"));
                Log("업데이트 설치 폴더 정리 완료");
            }

            private void RemoveUserData()
            {
                TryDeleteDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProductName));
                TryDeleteDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductName));
                Log("사용자 데이터 정리 완료");
            }

            private void ScheduleInstallDirectoryRemoval()
            {
                var installDir = Path.GetFullPath(_installDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!Directory.Exists(installDir) || installDir.Length < 10)
                {
                    return;
                }

                var marker = Path.Combine(installDir, "install-info.ini");
                var hasApp = Directory.GetFiles(installDir, ProductName + " V*.exe").Length > 0;
                if (!File.Exists(marker) && !hasApp)
                {
                    throw new InvalidOperationException("설치 폴더로 확인되지 않아 폴더 삭제를 중단했습니다.");
                }

                var runDir = Path.GetFullPath(_runDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!installDir.Equals(runDir, StringComparison.OrdinalIgnoreCase))
                {
                    Directory.Delete(installDir, true);
                    Log("설치 폴더 삭제 완료: " + installDir);
                    return;
                }

                var command = "/c timeout /t 2 /nobreak >nul & rmdir /s /q " + QuoteForCmd(installDir);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = command,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                Log("설치 폴더 삭제 예약: " + installDir);
            }

            private static string ResolveInstallDirectory()
            {
                var currentDir = AppDomain.CurrentDomain.BaseDirectory;
                var defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", UninstallProgram.ProductName);
                if (IsInstallDirectory(defaultDir))
                {
                    return defaultDir;
                }

                if (IsInstallDirectory(currentDir))
                {
                    return currentDir;
                }

                var updatedDir = ResolveUpdatedInstallDirectory();
                if (!string.IsNullOrWhiteSpace(updatedDir) && Directory.Exists(updatedDir))
                {
                    return updatedDir;
                }

                return currentDir;
            }

            private static string ResolveUpdatedInstallDirectory()
            {
                try
                {
                    var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    if (string.IsNullOrWhiteSpace(baseDir))
                    {
                        return string.Empty;
                    }

                    var currentPath = Path.Combine(baseDir, UninstallProgram.ProductName, "Installed", "current.txt");
                    if (!File.Exists(currentPath))
                    {
                        return string.Empty;
                    }

                    var appPath = File.ReadAllText(currentPath, Encoding.UTF8).Trim();
                    if (File.Exists(appPath))
                    {
                        return Path.GetDirectoryName(appPath);
                    }
                }
                catch
                {
                }

                return string.Empty;
            }

            private static bool IsInstallDirectory(string path)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                    {
                        return false;
                    }

                    var marker = Path.Combine(path, "install-info.ini");
                    var hasApp = Directory.GetFiles(path, UninstallProgram.ProductName + " V*.exe").Length > 0;
                    var hasUninstall = File.Exists(Path.Combine(path, "Uninstall.exe"));
                    return File.Exists(marker) || (hasApp && hasUninstall);
                }
                catch
                {
                    return false;
                }
            }

            private static void DeleteFileIfExists(string path)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
                }
            }

            private static void TryDeleteDirectory(string path)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                }
                catch
                {
                }
            }

            private static string QuoteForCmd(string value)
            {
                return "\"" + value.Replace("\"", "\\\"") + "\"";
            }

            private void Log(string message)
            {
                _logBox.Text = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine + _logBox.Text;
            }
        }
    }
}
