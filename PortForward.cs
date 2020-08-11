using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestProject1
{
    [TestClass]
    public class PortForward
    {
        [TestMethod]
        public void Test1() {
            Dictionary<int, IPEndPoint> ports = new Dictionary<int, IPEndPoint>() {
                { 1455, new IPEndPoint(IPAddress.Parse("10.169.1.16"), 1455) },
                { 1831, new IPEndPoint(IPAddress.Parse("10.169.1.16"), 1831) },
                { 800, new IPEndPoint(Dns.GetHostAddresses("www.baidu.com")[0], 80) }
            };

            foreach (var item in ports)
            {
                StateObject so = new StateObject()
                {
                    rule = item,
                    srv = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp),
                    cli = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                };
                so.srv.Bind(new IPEndPoint(IPAddress.Any, so.rule.Key));    //连接真实地址
                so.srv.Listen(0);                                           //监听

                so.srv.BeginAccept(SCAccept, so);                           //准备接收请求
                so.cli.BeginConnect(so.rule.Value, ar=>  {
                    var so = (StateObject)ar.AsyncState;
                    so.cli.EndConnect(ar);
                }, so);  //连接真实地址
            }

            Thread.Sleep(1000 * 60 * 60); //1个小时
        }

        private void SCAccept(IAsyncResult ar)
        {
            var so = (StateObject)ar.AsyncState;
            so.srvcli = so.srv.EndAccept(ar);   //开启一个新的工作 socket
            so.srv.BeginAccept(SCAccept, so);   //继续接受接入请求

            var t1 = Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        //读取请求
                        int len = so.srvcli.Receive(so.bufsrv, 0, StateObject.BUFFER_SIZE, SocketFlags.None, out SocketError _);

                        if (len == 0) break;

                        //转发真实IP
                        so.cli.Send(so.bufsrv, 0, len, SocketFlags.None);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        break;
                    }
                }
            });

            var t2 = Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        //读取真实IP返回
                        int len = so.cli.Receive(so.bufcli, 0, StateObject.BUFFER_SIZE, SocketFlags.None, out SocketError _);

                        if (len == 0) break;

                        //转发客户端
                        so.srvcli.Send(so.bufcli, 0, len, SocketFlags.None);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        break;
                    }
                }
            });

            Task.WaitAll(new Task[] { t1, t2 });
        }


        public class StateObject
        {
            public KeyValuePair<int, IPEndPoint> rule;

            public Socket srv = null;       //服务器
            public Socket srvcli = null;    //服务器工作socket
            public Socket cli = null;       //端口转发的socket

            public const int BUFFER_SIZE = 1024;
            public byte[] bufsrv = new byte[BUFFER_SIZE];
            public byte[] bufcli = new byte[BUFFER_SIZE];
        }
    }
}
