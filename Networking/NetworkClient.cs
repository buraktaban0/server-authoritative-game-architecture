using Tacmind.Collections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using Tacmind.Core;
using System.Linq;
using Tacmind.Logging;
using Tacmind.ResourceManagement;
using System.Threading.Tasks;

/*

	Outgoing queue is implemented because it's slightly faster to add the packet to the queue and let the sender thread send the packet than to send the packet on the main thread.

 */

namespace Tacmind.Networking
{

	public class NetworkClient
	{
		public int MaxPacketSize = 500;

		public int ReliableCheckInterval = 33;
		public int PingPongInterval = 1500;

		public Socket Socket { get; private set; }

		public IPEndPoint LocalEP { get; private set; }

		public ReliablePacketCollection reliablePackets;

		private ConcurrentDictionary<IPEndPoint, NetworkTarget> trustedTargets = new ConcurrentDictionary<IPEndPoint, NetworkTarget>();

		private BlockingCollection<NetworkStream> sendQueue;

		public ConcurrentQueue<NetworkStream> ownerlessPackets;

		public bool TrustedFilter { get; set; } = true;

		private Thread readThread;
		private bool shouldRead;

		private Thread writeThread;
		private bool shouldWrite;

		private NetworkStream writeBuffer, readBuffer, ackBuffer;


		private ushort rttPacketId = 0;

		private System.Threading.Timer reliableTimer;
		private System.Threading.Timer pingPongTimer;

		private Pool<NetworkStream> streamPool;
		private GeneralPool<IPEndPoint> epPool;

		private int simulateLag = -1;
		private float packetDropRatio = -1f;

		private System.Random rand = new System.Random();

		private Thread simulateThread;

		private BlockingCollection<NetworkStream> simulateQueue = new BlockingCollection<NetworkStream>(new ConcurrentQueue<NetworkStream>());

		public NetworkClient(IPEndPoint localEP, ConcurrentDictionary<IPEndPoint, NetworkTarget> trustedTargets)
		{
			if (Game.Instance.Args.HasArgument("simulate_lag"))
			{
				simulateLag = int.Parse(Game.Instance.Args.GetValue("simulate_lag"));
			}

			if (Game.Instance.Args.HasArgument("simulate_drop"))
			{
				packetDropRatio = float.Parse(Game.Instance.Args.GetValue("simulate_drop"));
			}

			ownerlessPackets = new ConcurrentQueue<NetworkStream>();

			streamPool = new Pool<NetworkStream>(1024 * 16);

			List<IPEndPoint> eps = new List<IPEndPoint>();
			for (int i = 0; i < 1024 * 16; i++)
			{
				eps.Add(new IPEndPoint(IPAddress.Any, 0));
			}

			epPool = new GeneralPool<IPEndPoint>(eps);

			LocalEP = localEP;

			sendQueue = new BlockingCollection<NetworkStream>(new ConcurrentQueue<NetworkStream>());

			foreach (var nt in trustedTargets.Values)
			{
				nt.SetClient(this);
				nt.SharedSendQueue = sendQueue;
			}

			this.trustedTargets = trustedTargets;


			Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

			Log.WriteLine("Socket TTL: " + Socket.Ttl);

			const int SIO_UDP_CONNRESET = -1744830452;
			byte[] inValue = new byte[] { 0 };
			byte[] outValue = new byte[] { 0 };
			Socket.IOControl(SIO_UDP_CONNRESET, inValue, outValue);

			Socket.Bind(localEP);

			LocalEP = (IPEndPoint)Socket.LocalEndPoint;

			writeBuffer = new NetworkStream(1024);
			readBuffer = new NetworkStream(1024);
			ackBuffer = new NetworkStream(512);


			reliablePackets = new ReliablePacketCollection();

			reliableTimer = new Timer(state => CheckReliablePackets(), null, 100, ReliableCheckInterval);


			if (NetworkManager.IsServer)
			{
				pingPongTimer = new Timer(state => PingPong(), null, 200, PingPongInterval);
			}

			shouldRead = true;
			shouldWrite = true;

			readThread = new Thread(Run_Read);
			writeThread = new Thread(Run_Write);

			readThread.Start();
			writeThread.Start();



			if (simulateLag > 0)
			{
				simulateThread = new Thread(Run_Simulate);
				simulateThread.Start();
			}

		}


		public void RegisterTarget(NetworkTarget nt)
		{
			nt.SetClient(this);
			nt.SharedSendQueue = sendQueue;

			if (!trustedTargets.ContainsKey(nt.RemoteEP))
			{
				trustedTargets[nt.RemoteEP] = nt;
			}
		}


		public void PingPong()
		{
			NetworkStream ns = new NetworkStream(32);
			foreach (var target in trustedTargets.Values)
			{
				ns.reliability = NetworkStream.Reliability.Unreliable;
				ns.dataType = NetworkStream.DataType.PingPong;
				ns.PacketId = ++rttPacketId;
				ns.RemoteEP = target.RemoteEP;
				ns.WriteFloat(target.RTTSmooth);
				ns.EndWrite();

				target.LastRttPacket = ns.PacketId;
				target.LastPingTime = NetworkUtility.GetUnixTimestamp();

				SendNow(ns);
			}
		}

		private void CheckReliablePackets()
		{
			reliablePackets.ClearAcknowledgedPackets();

			/*if (reliablePackets.packets.Count > 0)
			{
				Log.WriteErrorLine("REL: " + reliablePackets.packets.Count);
			}*/

			foreach (var ns in reliablePackets.GetPendingPackets())
			{
				SendImmediately(ns);
			}
		}

		public void RemoveTarget(NetworkTarget nt)
		{
			if (nt == null || trustedTargets == null || trustedTargets.ContainsKey(nt.RemoteEP) == false)
			{
				return;
			}

			NetworkTarget tempTarget;
			while (!trustedTargets.TryRemove(nt.RemoteEP, out tempTarget))
			{

			}

		}

		private void Run_Read()
		{
			try
			{
				EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
				while (shouldRead)
				{
					remoteEP = epPool.Get();

					//				Log.WriteErrorLine("read");

					NetworkStream ns = streamPool.Get(); // new NetworkStream(512);
					int c = Socket.ReceiveFrom(ns.Bytes, ref remoteEP);

					ns.receivedAt = NetworkUtility.Time;

					ns.Size = c - NetworkStream.HEADER_SIZE;


					ns.EndReceive();

					if (!trustedTargets.ContainsKey((IPEndPoint)remoteEP))
					{
						//Log.WriteLine("Ownerless Packet Received - From "  + remoteEP + "  " + ns.dataType);
						ns.RemoteEP = (IPEndPoint)remoteEP;
						ownerlessPackets.Enqueue(ns);
						continue;
					}

					//Log.WriteLine("Packet Received - From " + remoteEP + "  " + ns.dataType);

					NetworkTarget nt = trustedTargets[(IPEndPoint)remoteEP];

					ns.RemoteEP = nt.RemoteEP;

					if (ns.dataType == NetworkStream.DataType.PingPong)
					{
						if (NetworkManager.IsServer)
						{
							if (ns.PacketId == nt.LastRttPacket)
							{
								float timeDifference = (NetworkUtility.GetUnixTimestamp() - nt.LastPingTime);
								nt.UpdateRTT(timeDifference);
							}
						}
						else
						{
							nt.UpdateRTT(ns.ReadFloat());
							ns.Size -= 4;
							SendNow(ns);
						}

						//Log.WriteErrorLine("PING PONG: " + remoteEP.ToString());
						continue;

					}

					NetworkStream.Reliability rel = ns.reliability;
					ushort packetId = ns.PacketId;

					if (rel == NetworkStream.Reliability.Unreliable) //Unreliable
					{
						//Log.WriteErrorLine("UNREL:  " + remoteEP.ToString());
						nt.EnqueuePacket(ns);
						//nt.ReceiveQueue.Enqueue(ns);
					}
					else if (rel == NetworkStream.Reliability.Reliable) // Reliable
					{
						ackBuffer.Clear();
						ackBuffer.reliability = NetworkStream.Reliability.Acknowledgement;
						ackBuffer.PacketId = ns.PacketId;
						ackBuffer.Part = ns.Part;
						ackBuffer.TotalParts = ns.TotalParts;
						ackBuffer.dataType = ns.dataType;
						ackBuffer.RemoteEP = ns.RemoteEP;
						ackBuffer.EndWrite();

						SendNow(ackBuffer);

						//Log.WriteErrorLine(ns.dataType + "  " + ns.Part + "  " + ns.TotalParts);
						//Log.WriteErrorLine("REL: " + remoteEP.ToString());
						
						nt.EnqueuePacket(ns);
						//nt.ReceiveQueue.Enqueue(ns);
					}
					else if (rel == NetworkStream.Reliability.Acknowledgement) // Acknowledgement
					{
						//Log.WriteErrorLine("ACK: " + remoteEP.ToString());
						ns.reliability = NetworkStream.Reliability.Reliable;
						ns.Owner = nt.Id;
						ns.EndWrite();
						reliablePackets.Remove(ns.UId);
					}

				}

			}
			catch (Exception ex)
			{
				Log.WriteErrorLine("NetClient.Run_Receive -> " + ex.ToString());
			}
		}

		private void Run_Write()
		{
			try
			{
				while (shouldWrite)
				{

					NetworkStream ns = sendQueue.Take();


					if (ns.Size > MaxPacketSize)
					{
						foreach (var part in ns.Split(MaxPacketSize))
						{
							SendNow(part);
						}


						continue;
					}


					SendNow(ns);

				}
			}
			catch (Exception ex)
			{
				Log.WriteErrorLine("NetClient.Run_Write -> " + ex.ToString());
			}
		}

		private void Run_Simulate()
		{
			try
			{
				while (shouldWrite)
				{
					var ns = simulateQueue.Take();

					var time = NetworkUtility.GetUnixTimestamp();

					int diff = (int)(ns.time - time);

					if (diff > 1)
					{
						Thread.Sleep(diff);
					}

					SendNoLag(ns);

				}
			}
			catch (Exception ex)
			{

			}

		}

		public void SendNoLag(NetworkStream ns)
		{
			Socket.SendTo(ns.Bytes, 0, ns.Size + NetworkStream.HEADER_SIZE, SocketFlags.None, ns.RemoteEP);
		}

		public void SendImmediately(NetworkStream ns)
		{
			if (packetDropRatio > 0f)
			{
				if (rand.NextDouble() < packetDropRatio)
				{
					return;
				}
			}

			if (simulateLag <= 0)
			{
				Socket.SendTo(ns.Bytes, 0, ns.Size + NetworkStream.HEADER_SIZE, SocketFlags.None, ns.RemoteEP);
			}
			else
			{
				ns.time = NetworkUtility.GetUnixTimestamp() + simulateLag;
				simulateQueue.Add(ns);
			}
		}

		public void SendNow(NetworkStream ns)
		{
			//Log.WriteLine(ns.dataType + "   " + ns.Part + " / " + ns.TotalParts + "  " + ns.Size + " | " + ns.Bytes.Length);

			//Log.WriteErrorLine("SENDING TO " + ns.RemoteEP);
			//Console.WriteLine("SENDING TO " + ns.RemoteEP);

			SendImmediately(ns);

			if (ns.IsReliable)
			{
				var copy = ns.GetCopy();
				reliablePackets.Add(copy);
			}
		}



		public void Shutdown()
		{
			reliableTimer?.Change(Timeout.Infinite, Timeout.Infinite);
			reliableTimer?.Dispose();

			pingPongTimer?.Change(Timeout.Infinite, Timeout.Infinite);
			pingPongTimer?.Dispose();

			shouldWrite = false;
			shouldRead = false;

			simulateQueue?.CompleteAdding();

			sendQueue?.CompleteAdding();

			Socket?.Shutdown(SocketShutdown.Both);
			Socket?.Close(1);

			writeThread?.Join(500);
			readThread?.Join(500);
		}

	}

}