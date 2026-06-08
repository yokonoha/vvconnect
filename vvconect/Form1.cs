using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace vvconect
{
   

    public partial class Form1 : Form
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private static readonly HttpClient httpClient = new HttpClient();
        private const string vvendpoint = "http://localhost:50021";
        private int speakerId = 1;//デフォ 漆黒
        private double spcspd = 1.0;
        private Dictionary<string, int> wasya_name_and_id = new Dictionary<string, int>();
        
        public Form1()
        {
            InitializeComponent();
            _ = extserverstart();
        }

        private async Task extserverstart()
        {
            //receive
            await Task.Run(async () =>
            {
                using (var server = new NamedPipeServerStream("extlib", PipeDirection.In))
                {
                    while (true)
                    {
                        try
                        {
                            await server.WaitForConnectionAsync();
                            using (var reader = new StreamReader(server, Encoding.UTF8, true, 1024, leaveOpen: true))
                            {
                                string message = await reader.ReadLineAsync();
                                //受信したメッセージを処理
                                this.Invoke(new Action(() =>
                                {
                                    if(message.StartsWith("read:"))
                                    {
                                        mkjson(message.Substring(5));
                                        answerbacker2(message.Substring(5));
                                    }
                                    else if (message.StartsWith("chksupportedsoftware"))
                                    {
                                        answerbacker();
                                        Console.WriteLine("代読くんから通達指令");
                                    }

                                }));
                            }
                            if (server.IsConnected)
                            {
                                server.Disconnect();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"本体からの受信に失敗: {ex.Message}"); Debug.WriteLine(ex.Message);
                            await Task.Delay(1000);
                        }
                    }
                }
            },_cts.Token);
        }
        /// <summary>
        /// 代読くんに対応ソフトを通達する関数。
        /// </summary>
        async void answerbacker()
        {
            using (var client = new NamedPipeClientStream(".", "extlib2", PipeDirection.Out))
            {
                try
                {
                    client.Connect(2000);
                    using (var writer = new StreamWriter(client))
                    {
                        writer.AutoFlush = true;
                        writer.WriteLine("supportedsoftware:VOICEVOX");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"本体への送信に失敗: {ex.Message}"); Debug.WriteLine(ex.Message);
                }
            }
        }

        async void answerbacker2(string text)
        {
            using (var client = new NamedPipeClientStream(".", "extlib2", PipeDirection.Out))
            {
                try
                {
                    client.Connect(2000);
                    using (var writer = new StreamWriter(client))
                    {
                        writer.AutoFlush = true;
                        writer.WriteLine($"log:ボイボコネクトプラグイン(発話):{text}");
                        answerbacker();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"本体への送信に失敗: {ex.Message}"); Debug.WriteLine(ex.Message);
                }
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            storewasya();
           
        }

        private async void mkjson(string text)
        {
            var json = $"{{\"text\":\"{text}\"}}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try
            {
            
                var response = await httpClient.PostAsync($"{vvendpoint}/audio_query?text={Uri.EscapeDataString(text)}&speaker={speakerId}", content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();

                 
                    JObject queryJson = JObject.Parse(responseBody);
                    queryJson["speedScale"] = spcspd; 

                 
                    string modified = queryJson.ToString();


                    await GenaWavAudioF(modified);
                }
                else
                {
                    Console.WriteLine($"リクエスト失敗(audio_query): {response.StatusCode}");
                    errorreporter($"発話パラメータ設定の取得に失敗しました。開発者向け詳細ログはこちらです。=>:RES is {response.StatusCode} POSITION: mkjson HTTP RES err");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"エラー発生(mkjson): {ex.Message}");
                errorreporter($"発話パラメータ設定の取得に失敗しました。開発者向け詳細ログはこちらです。=>:{ex.Message} POSITION: mkjson ex catch err");
            }
        }
        private async Task<string> GenaWavAudioF(string queryJsonText)
        {
            var content = new StringContent(queryJsonText, Encoding.UTF8, "application/json");
            try
            {
                var response = await httpClient.PostAsync($"{vvendpoint}/synthesis?speaker={speakerId}", content);
                if (response.IsSuccessStatusCode)
                {
                    byte[] audioData = await response.Content.ReadAsByteArrayAsync();
                    PlayAudiofrommemory(audioData);
                    return null;
                }
                else
                {
                    Console.WriteLine($"リクエスト失敗(synthesis): {response.StatusCode}");
                    errorreporter($"音声の再生に失敗しました。開発者向け詳細ログはこちらです。=>:RES is {response.StatusCode}.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"エラー発生(GenaWavAudioF): {ex.Message}");
                errorreporter($"音声の再生に失敗しました。開発者向け詳細ログはこちらです。=>:{ex.Message} (問題が何度も発生する場合は開発者までお知らせください)");
                return null;
            }
        }

        private void PlayAudiofrommemory(byte[] audioData)
        {
            using (var ms = new MemoryStream(audioData))
            {
                using (var player = new System.Media.SoundPlayer(ms))
                {
                    player.PlaySync();
                }
            }
        }


        async void storewasya()
        {
            try
            {
                var response = await httpClient.GetAsync($"{vvendpoint}/speakers");
                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var speakers = JsonConvert.DeserializeObject<List<Speaker>>(responseBody);

                    spinner1.Items.Clear();

                    foreach (var speaker in speakers)
                    {
                        foreach (var style in speaker.styles)
                        {
                            string key = $"{speaker.name}({style.name})";
                            if (!wasya_name_and_id.ContainsKey(key))
                            {
                                wasya_name_and_id.Add(key, style.id);
                                spinner1.Items.Add(key);
                            }
                        }
                    }

                    
                    if (spinner1.Items.Count > 0)
                    {
                        spinner1.SelectedIndex = 0;
                    }
                    toolStripStatusLabel1.Text = "接続完了";
                }
                else
                {
                    Console.WriteLine($"リクエスト失敗: {response.StatusCode}");
                    errorreporter($"話者の取得に失敗しました。VOICEVOXの起動が完了していない可能性やその他の問題が発生している可能性があります。開発者向け詳細ログはこちらです。=>:RES is {response.StatusCode}.");
                    storewasya();
                    toolStripStatusLabel1.Text = "データ取得失敗 E1 (再度通信中)";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"エラー発生: {ex.Message}");
                errorreporter($"話者の取得に失敗しました。VOICEVOXの起動が完了していない可能性やその他の問題が発生している可能性があります。開発者向け詳細ログはこちらです。=>:{ex.Message}");
                storewasya();
               toolStripStatusLabel1.Text = "データ取得失敗 E2 (再度通信中)";
            }
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }



        private void spinner1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (spinner1.SelectedItem != null)
            {
                string selectedKey = spinner1.SelectedItem.ToString();
                if (wasya_name_and_id.TryGetValue(selectedKey, out int id))
                {
                    speakerId = id; // ID更新
                    Console.WriteLine($"話者を変更しました: {selectedKey} (ID: {speakerId})");
                    toolStripStatusLabel3.Text = $"選択中の音声クレジット表記 | VOICEVOX: {selectedKey}";
                }
            }
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            spcspd = trackBar1.Value / 10.0;
            labelSpeed.Text = $"{spcspd:F1}";
        }

        private void errorreporter(string msg)
        {
            using (var client = new NamedPipeClientStream(".", "extlib2", PipeDirection.Out))
            {
                try
                {
                    client.Connect(2000);
                    using (var writer = new StreamWriter(client))
                    {
                        writer.AutoFlush = true;
                        writer.WriteLine($"log:[エラー]ボイボコネクトプラグイン:{msg}");
                        answerbacker();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"本体への送信に失敗: {ex.Message}"); Debug.WriteLine(ex.Message);
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.Application.Restart();
        }

        private void toolStripStatusLabel2_Click(object sender, EventArgs e)
        {
            osl osl = new osl();
            osl.Show();
        }

        private void toolStripStatusLabel3_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(toolStripStatusLabel3.Text.Substring(16)); // "選択中の音声クレジット表記 | "分のやつです
        }
    }
    public class Speaker
    {
        public string name { get; set; }
        public string speakerId { get; set; }
        public List<Style> styles { get; set; }
    }


    public class Style
    {
        public string name { get; set; }
        public int id { get; set; }
    }
}
