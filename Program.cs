using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Linq;

namespace Client
{
    class Program
    {
        private static byte[] result = new byte[1024];
        static Socket clientSocket;

        private static void ReciveMsg(object obj, MsgEventArgs msgEventArgs)
        {
            Console.WriteLine(msgEventArgs.Msg);
        }

        static void Main1(string[] args)
        {
            // var dy = new DYComm("2298474");//56040
            var dy = new DYComm(Console.ReadLine());
            dy.SendMsg += ReciveMsg;
            dy.Start();
            Console.Read();
            return;
        }
        /// <summary>
        ///  通过clientSocket接收数据  
        /// </summary>
        static void ReceiveMessage()
        {
            while (true)
            {
                try
                {
                    int receiveLength = clientSocket.Receive(result);
                    if (receiveLength > 0)
                    {
                        var res = Encoding.UTF8.GetString(result, 12, receiveLength - 13);
                        var res0 = Encoding.UTF8.GetString(result, 0, receiveLength);
                        Console.WriteLine("接收服务器消息0：{0}\r\n{1}", res, res0);
                    }
                    result = new byte[1024];
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
        /// <summary>
        /// 通过 clientSocket 发送数据 
        /// </summary>
        static void SendMessage()
        {
            while (true)
            {
                try
                {
                    var str = "2298474";
                    Console.WriteLine("向服务器发送消息");
                    byte[] message;
                    switch (Console.ReadLine())
                    {
                        case "a": { message = GetV(string.Format("type@=loginreq/roomid@={0}/", str)); } break;
                        case "b": { message = GetV(string.Format("type@=joingroup/rid@={0}/gid@=-9999/", str)); } break;
                        case "c": { message = GetV("type@=mrkl/"); } break;
                        default:
                            {
                                message = new byte[0];
                            }
                            break;
                    }
                    clientSocket.Send(message);
                }
                catch
                {
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close();
                }
            }
        }

        public static byte[] GetV(string para)
        {
            para += '\0';
            // para = para.Replace("@", "@A").Replace("/","@S");
            var msgs = Encoding.UTF8.GetBytes(para);
            // string msgs = "aaa";
            byte[] msg = msgs;// Encoding.Default.GetBytes(msgs);
            int length = msg.Length;
            int lengthall = (int)(length + 12);
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
    }
}
