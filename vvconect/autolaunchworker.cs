using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace vvconect
{
    public partial class autolaunchworker : Form
    {
        public autolaunchworker()
        {
            InitializeComponent();
        }

        private void autolaunchworker_Load(object sender, EventArgs e)
        {
            string voicevoxPath = Properties.Settings.Default.vpath;

            if (string.IsNullOrEmpty(voicevoxPath) || !File.Exists(voicevoxPath))
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string defaultPath = Path.Combine(appData, @"Programs\VOICEVOX\VOICEVOX.exe");

                if (File.Exists(defaultPath))
                {
                    voicevoxPath = defaultPath;
                }
            }

           
            if (!string.IsNullOrEmpty(voicevoxPath) && File.Exists(voicevoxPath))
            {
               
                if (Process.GetProcessesByName("run").Length == 0 && Process.GetProcessesByName("VOICEVOX").Length == 0)
                {
                    try
                    {
                        Process.Start(voicevoxPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"VOICEVOXの起動に失敗しました: {ex.Message}");
                    }
                }
            }
            else
            {
               
                MessageBox.Show("VOICEVOXの起動ファイル(VOICEVOX.exe)が見つかりませんでした。\n次の画面でVOICEVOX.exeの場所を指定してください。");

                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "VOICEVOX起動ファイル (VOICEVOX.exe)|VOICEVOX.exe";
                    ofd.Title = "VOICEVOXのVOICEVOX.exeを選択してください";

                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        
                        Properties.Settings.Default.vpath = ofd.FileName;
                        Properties.Settings.Default.Save();
                        Process.Start(ofd.FileName);
                    }
                }
            }
            Form1 form1 = new Form1();
            form1.ShowDialog();
            this.Close();
        }

    }
}
