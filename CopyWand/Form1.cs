using IronOcr;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CopyWand
{
    public partial class Form1 : Form
    {
        string rootPath = Directory.GetParent(Application.ExecutablePath).ToString();
        int mov, movX, movY;

        [DllImport("User32.dll")]
        static extern IntPtr FindWindow(String sClassName, String sAppName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);


        bool snippingMode = false;
        Point[] points;
        string default_cursor;
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == 0x0312)
            {
                int id = m.WParam.ToInt32();
                if (id == 1)
                {
                    snippingMode = !snippingMode;
                    if (snippingMode)
                    {
                        points = null;
                        default_cursor = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Cursors\", "Arrow", null).ToString();
                        //SetCrossArrow();
                    }
                    else
                    {
                        label5.Text = "";
                        label6.Text = "";
                        SetDefaultCursor();
                    }

                    label3.Text = "Snipping Mode: " + snippingMode.ToString();

                    lblStatus.ForeColor = Color.Red;
                    lblStatus.Text = "Auswählen";
                }
                else if (id == 2)
                {
                    if (!snippingMode)
                        return;
                    if (points == null)
                    {
                        points = new Point[2];
                        //points[0].X = MousePosition.X;
                        //points[0].Y = MousePosition.Y;
                        points[0] = Cursor.Position;
                        label5.Text = $"{points[0].X}/{points[0].Y} (X/Y)";
                    }
                    else
                    {
                        //points[1].X = MousePosition.X;
                        //points[1].Y = MousePosition.Y;
                        points[1] = Cursor.Position;
                        label6.Text = $"{points[1].X}/{points[1].Y} (X/Y)";
                        Screenshot(points[0], points[1]).Save("tempOcrIMG.png", ImageFormat.Png);
                        new Thread(()=>CopyTextFromImageToClipboard("tempOcrIMG.png")).Start();
                        
                        label5.Text = "";
                        label6.Text = "";
                        points = null;
                    }
                }
            }
        }

        private void SetDefaultCursor()
        {
            string curFile = default_cursor;
            if (!File.Exists(curFile))
                return;
            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Cursors\", "Arrow", curFile);
            SystemParametersInfo(SPI_SETCURSORS, 0, 0, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        }

        private void SetCrossArrow()
        {
            string curFile = Environment.GetFolderPath(Environment.SpecialFolder.Windows) + "\\cursors\\cross_i.cur";
            if (!File.Exists(curFile))
                return;
            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Cursors\", "Arrow", curFile);
            SystemParametersInfo(SPI_SETCURSORS, 0, 0, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        }

        const int SPI_SETCURSORS = 0x0057;
        const int SPIF_UPDATEINIFILE = 0x01;
        const int SPIF_SENDCHANGE = 0x02;

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfo")]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, uint pvParam, uint fWinIni);

        public Form1()
        {
            InitializeComponent();
            fileDialog.CheckFileExists = true;

            RegisterHotKey(Handle, 2, (uint)0, (uint)Keys.F2);
            RegisterHotKey(Handle, 1, (uint)0x0002, (uint)Keys.F2);
        }

        public static Bitmap Screenshot(Point A, Point B)
        {
            int imagewidth = Math.Abs(A.X - B.X);
            int imageheight = Math.Abs(A.Y - B.Y);
            var bmpScreenshot = new Bitmap(imagewidth, imageheight);

            var gfxScreenshot = Graphics.FromImage(bmpScreenshot);

            Size s = new Size(imagewidth, imageheight);
            gfxScreenshot.CopyFromScreen(new Point(Math.Min(A.X, B.X), Math.Min(A.Y, B.Y)), new Point(0, 0), s);

            return bmpScreenshot;
        }

        private void CopyTextFromImageToClipboard(string filePath)
        { 
            CheckForIllegalCrossThreadCalls = false;
            var info = new ProcessStartInfo();
            info.FileName = "python.exe";
            string script = "ocr.py";

            info.Arguments = $"\"{script}\" \"{filePath}\"";
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;

            string result = "";

            using (var process = Process.Start(info))
            {
                result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
            }

            //MessageBox.Show(result);
            if (result != "ERROR")
            {
                if (result != "")
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        Clipboard.SetText(result);
                        lblStatus.ForeColor = Color.Green;
                        lblStatus.Text = "Kopiert";
                    });
                }
                else
                {
                    MessageBox.Show("Das Ergebnis ist leer.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                this.Invoke((MethodInvoker)delegate
                {
                    lblStatus.ForeColor = Color.Red;
                    lblStatus.Text = "Error";
                });
            }
            CheckForIllegalCrossThreadCalls = true;
        }

        #region events
        private void LblDragBar_MouseDown(object sender, MouseEventArgs e)
        {
            mov = 1;
            movX = e.X;
            movY = e.Y;
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            Application.Exit();
            UnregisterHotKey(Handle, 1);
        }

        private void BtnMinimize_Click(object sender, EventArgs e)
        {
            notifyIcon.Visible = true;
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
        }

        private void BtnOpenFileDialog_Click(object sender, EventArgs e)
        {
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                new Thread(() => CopyTextFromImageToClipboard(fileDialog.FileName)).Start();

                //MessageBox.Show("Die Erkennung ist beendet.", "Fertig", MessageBoxButtons.OK);
                //ResultTextForm resultForm = new ResultTextForm(result);
            }
        }

        private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            notifyIcon.Visible = false;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            SetDefaultCursor();
        }

        private void label3_Click(object sender, EventArgs e)
        {
            snippingMode = !snippingMode;
            if (snippingMode)
            {
                points = null;
                default_cursor = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Cursors\", "Arrow", null).ToString();
                //SetCrossArrow();
            }
            else
            {
                label5.Text = "";
                label6.Text = "";
                SetDefaultCursor();
            }

            label3.Text = "Snipping Mode: " + snippingMode.ToString();

            lblStatus.ForeColor = Color.Red;
            lblStatus.Text = "Auswählen";
        }

        private void LblDragBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (mov == 1)
            {
                SetDesktopLocation(MousePosition.X - movX, MousePosition.Y - movY);
            }
        }

        private void LblDragBar_MouseUp(object sender, MouseEventArgs e)
        {
            mov = 0;
        }
        #endregion
    }

}
