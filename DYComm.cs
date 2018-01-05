using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Client
{
    public class In
    {
        private static void ReciveMsg(object obj, MsgEventArgs msgEventArgs)
        {
            Console.WriteLine(msgEventArgs.Msg);
        }
        static void Main(string[] args)
        {
            var dy = new DYComm(Console.ReadLine());//56040
            dy.SendMsg += ReciveMsg;
            dy.Start();
            Console.Read();
            return;
        }
    }
    public class DYComm
    {
        private Socket _socket;
        private NetworkStream _networkStream;
        private string _roomId;
        private string _groupId = "-9999";
        private string _hostName = "openbarrage.douyutv.com";
        private int _port = 8601;
        private EventHandler<MsgEventArgs> _sendMsg;
        public event EventHandler<MsgEventArgs> SendMsg
        {
            add
            {
                _sendMsg += value;
            }
            remove
            {
                _sendMsg -= value;
            }
        }
        public DYComm(string roomId)
        {
            _roomId = roomId;
        }
        public DYComm(string roomId, string groupId)
        {
            _roomId = roomId;
            _groupId = groupId;
        }
        public void Start()
        {
            Init();
            KeepAlive();
            Revice();
        }
        private void Init()
        {
            _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ipe = new IPEndPoint(Dns.GetHostAddresses(_hostName)[0].MapToIPv4(), _port);
            _socket.Connect(ipe);
            _networkStream = new NetworkStream(_socket);
            if (!_socket.Connected)
            {
                Send("链接不成功");
                return;
            }
            var byts = GetParam(string.Format("type@=loginreq/roomid@={0}/", _roomId));
            _networkStream.Write(byts, 0, byts.Length);
            _networkStream.Flush();
            //初始化弹幕服务器返回值读取包大小
            byte[] recvByte = new byte[4096];
            //获取弹幕服务器返回值
            _networkStream.Read(recvByte, 0, recvByte.Length);
            if (recvByte.Length <= 12)
            {
                Send("登陆失败");
                return;
            }
            string dataStr = Encoding.UTF8.GetString(recvByte.Skip(12).ToArray());
            if (dataStr.Contains("type@=loginres"))
            {
                Send("登陆成功");
            }

            byts = GetParam(string.Format("type@=joingroup/rid@={0}/gid@={1}/", _roomId, _groupId));
            _networkStream.Write(byts, 0, byts.Length);
            _networkStream.Flush();
        }

        private void KeepAlive()
        {
            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        var byts = GetParam(string.Format("type@=mrkl/"));
                        _networkStream.Write(byts, 0, byts.Length);
                        _networkStream.Flush();
                        Send("保持存活");
                    }
                    catch
                    {

                    }
                    Thread.Sleep(40 * 1000);
                }

            }).Start();
        }


        private void Revice()
        {
            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        byte[] reciveByts = new byte[4096];
                        var reciveBytslength = _networkStream.Read(reciveByts, 0, reciveByts.Length);
                        if (reciveBytslength < 12)
                        {
                            continue;
                        }
                        byte[] realBuf = new byte[reciveBytslength];
                        Array.Copy(reciveByts, realBuf, reciveBytslength);
                        var dataStr = Encoding.UTF8.GetString(realBuf).Substring(12);
                        var datas = AnalysisReviceData(dataStr);
                        datas.ForEach(item =>
                        {
                            var msg = "";
                            item["nn"] = CNPadRight(item["nn"]);
                            switch (item["type"])
                            {
                                case "chatmsg":
                                    {
                                        msg = string.Format("{0}-消息：{1}", item["nn"], item["txt"]);
                                        break;
                                    }
                                case "dgb":
                                    {
                                        msg = string.Format("{0}-礼物：{1}，个数：{2}", item["nn"], (item["bg"] == "0" ? "小礼物" : "大礼物"), item["gfcnt"]);
                                        break;
                                    }
                                case "uenter":
                                    {
                                        msg = string.Format("{0}-来到房间", item["nn"]);
                                        break;
                                    }
                                default:
                                    break;
                            }
                            if (!string.IsNullOrEmpty(msg))
                            {
                                Send(msg);
                            }
                        });

                    }
                    catch (Exception ex)
                    {

                    }
                }

            }).Start();
        }

        private List<Dictionary<string, string>> AnalysisReviceData(string reviceData)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();
            List<Dictionary<string, string>> datas = new List<Dictionary<string, string>>();
            while (true)
            {
                if (reviceData.Contains("type@="))
                {
                    var postion = reviceData.LastIndexOf("type@=");
                    var datatemp = reviceData.Substring(postion);
                    // datas.Add(datatemp);
                    reviceData = reviceData.Remove(postion);

                    var datatemps = datatemp.Split('/');
                    if (datatemps.Length > 0)
                    {
                        for (int i = 0; i < datatemps.Length; i++)
                        {
                            var delimiterPostion = datatemps[i].IndexOf("@=");
                            if (delimiterPostion > 0)
                            {
                                // datatemps[i].Substring("@=");
                                var key = datatemps[i].Substring(0, delimiterPostion)
                                    .Replace("@A", "@").Replace("@S", "/");// 
                                var value = datatemps[i].Substring(delimiterPostion + 2, datatemps[i].Length - delimiterPostion - 2)
                                    .Replace("@A", "@").Replace("@S", "/");
                                data.Add(key, value);
                            }
                        }
                        datas.Add(data);
                    }
                }
                else
                {
                    break;
                }
            }
            datas.Reverse();
            return datas;
        }

        private void Send(string msg)
        {
            _sendMsg?.Invoke(this, new MsgEventArgs(msg));
        }

        public byte[] GetParam(string para)
        {
            para += '\0';
            var msgs = Encoding.UTF8.GetBytes(para);
            byte[] msg = msgs;
            int length = msg.Length;
            int lengthall = length + 8;//12
            byte[] lengthByte = BitConverter.GetBytes(lengthall);//short转字节调换位置
            byte[] type = BitConverter.GetBytes((short)689);//short转字节调换位置
            byte[] bt = { 0, 0 };
            byte[] all = lengthByte
            .Concat(lengthByte)
            .Concat(type)
            .Concat(bt)
            .Concat(msg)
            .ToArray();
            return all;

        }

        private static string CNPadRight(string str, int total = 20)
        {
            var strByts = Encoding.Default.GetBytes(str);
            var res = str.PadRight(total - strByts.Length + str.Length, ' ');
            return res;
        }

    }
    public class MsgEventArgs : EventArgs
    {
        public MsgEventArgs(string msg)
        {
            Msg = msg;
        }
        public string Msg { get; set; }
    }
}