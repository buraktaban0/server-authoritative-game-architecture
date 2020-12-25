using Tacmind.Core;
using Tacmind.Inputs;
using Tacmind.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;
using System.Collections.Concurrent;
using Tacmind.ResourceManagement;
using Tacmind.Characters;
using Tacmind.MatchMaking;

namespace Tacmind.Networking
{

	public static class NetworkManager
	{
		public static readonly int MIN_PLAYER_ID = 1;

		public static ushort LocalId { get; set; } = 2;

		public static bool IsServer { get; set; }
		public static bool IsClient { get { return !IsServer; } }

		public static bool IsConnected { get; set; }

		public static bool IsInitialized { get { return Client != null; } }

		public static Dictionary<ushort, INetworkEntity> Entities { get; private set; } = new Dictionary<ushort, INetworkEntity>();

		public static ConcurrentDictionary<IPEndPoint, NetworkTarget> TargetsShared { get; private set; } = new ConcurrentDictionary<IPEndPoint, NetworkTarget>();
		public static List<NetworkTarget> Targets { get; private set; } = new List<NetworkTarget>();

		public static NetworkClient Client { get; private set; }

		public static int Port { get; private set; } = 0;

		public static IPEndPoint LocalEP { get; private set; } = new IPEndPoint(IPAddress.Any, Port);

		public static IOwnerlessPacketHandler OwnerlessPacketHandler { get; set; }
		public static INetworkEventHandler EventHandler { get; set; }

		private static Dictionary<NetworkEventType, Action<NetworkTarget, NetworkEventType, NetworkStream>> localHandlers = new Dictionary<NetworkEventType, Action<NetworkTarget, NetworkEventType, NetworkStream>>();

		private static Queue<NetworkStream> pendingEvents = new Queue<NetworkStream>(64);


		private static List<InstantiateData> instantiateHistory = new List<InstantiateData>(64);


		public static string serverIP = "127.0.0.1";
		public static int serverPort = 0; //10710;

		public static IPEndPoint serverEP = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

		private static Pool<NetworkStream> streamPool;

		private static void SetupLocalHandlers()
		{
			localHandlers?.Clear();
			localHandlers = new Dictionary<NetworkEventType, Action<NetworkTarget, NetworkEventType, NetworkStream>>();
			localHandlers[NetworkEventType.None] = OnEvent_None;
			localHandlers[NetworkEventType.Instantiate] = OnEvent_Instantiate;
			localHandlers[NetworkEventType.Destroy] = OnEvent_Destroy;
		}


		public static void Bind()
		{
			LocalEP = new IPEndPoint(IPAddress.Any, 0);
			BindAt(LocalEP);
		}

		public static void BindAt(IPEndPoint ep)
		{
			Client = new NetworkClient(ep, TargetsShared);
			LocalEP = Client.LocalEP;
		}

		public static void StartServer()
		{
			streamPool = new Pool<NetworkStream>(1024 * 8);

			IsServer = true;

			IsConnected = true;

			SetupLocalHandlers();

			/*
			LocalEP = new IPEndPoint(IPAddress.Any, serverPort);
			Client = new NetworkClient(LocalEP, TargetsShared);
			LocalEP = Client.LocalEP;
			*/
		}


		public static void StartClientSilent()
		{

			streamPool = new Pool<NetworkStream>(1024 * 8);

			LocalId = 0;

			IsServer = false;

			SetupLocalHandlers();

			/*Targets = new List<NetworkTarget>();
			TargetsShared = new ConcurrentDictionary<IPEndPoint, NetworkTarget>();*/


			NetworkTarget nt = new NetworkTarget(serverEP, "NONE", 0);
			Targets.Add(nt);
			TargetsShared[nt.RemoteEP] = nt;
			Client.RegisterTarget(nt);
		}

		public static void StartClient()
		{
			StartClientSilent();

			/*
			LocalEP = new IPEndPoint(IPAddress.Any, 0);
			Client = new NetworkClient(LocalEP, TargetsShared);
			LocalEP = Client.LocalEP;*/

			var ns = BeginEvent(NetworkEventType.Connect);

			/*ns.WriteByte(61);
			ns.WriteByte(61);
			ns.WriteByte(61);*/

			//ns.WriteByte((byte)ClientGameLoop.LocalTeam);
			ns.WriteString(Matchmaking.LocalPlayerKey);

			string name = "UnknownPlayer";
			if (Game.Instance.Args.HasArgument("player_name"))
			{
				name = Game.Instance.Args.GetValue("player_name");
			}

			ns.WriteString(ClientArenaPhase.LocalCharacter);
			ns.WriteString(name);
			ns.reliability = NetworkStream.Reliability.Unreliable;

			var nt = Targets[0];

			nt.Send(ns);


		}


		public static void CheckConnections()
		{


			if (IsClient)
			{
				if (!IsConnected)
				{
					return;
				}

				if (Targets[0].SilentTime > 25 * 1000)
				{
					Log.WriteErrorLine("Server was silent for over 10 seconds.");
					Game.Instance.ResetGame();
				}

				return;
			}

			List<NetworkTarget> removed = new List<NetworkTarget>();
			List<ushort> removedEntities = new List<ushort>();
			foreach (var nt in Targets)
			{
				if (nt.SilentTime > 10 * 1000)
				{
					removed.Add(nt);

					Log.WriteErrorLine("Player " + nt.Id + " has disconnected due to 10 seconds of silence.");

					for (int i = instantiateHistory.Count - 1; i >= 0; i--)
					{
						if (instantiateHistory[i].Owner == nt.Id)
						{
							instantiateHistory.RemoveAt(i);
						}
					}

					foreach (var entity in Entities.Values)
					{
						if (entity.GetOwnerId() == nt.Id)
						{
							removedEntities.Add(entity.GetEntityId());
						}
					}
				}
			}

			foreach (var id in removedEntities)
			{
				if (Entities.ContainsKey(id) == false)
					continue;

				INetworkEntity entity = Entities[id];

				if ((entity as NetworkBehaviour) != null)
				{
					Destroy((entity as NetworkBehaviour).gameObject);
				}

				if (Entities.ContainsKey(id) == false)
					continue;

				Entities.Remove(id);
			}

			foreach (var nt in removed)
			{
				NetworkTarget n;
				TargetsShared.TryRemove(nt.RemoteEP, out n);
				Targets.Remove(nt);
				Client.RemoveTarget(nt);
			}
		}

		public static NetworkTarget AddPlayer(IPEndPoint ep, string playerSessionId, Game.Team team)
		{
			if (TargetsShared.ContainsKey(ep))
			{
				Log.WriteErrorLine("NetworkManager.AddPlayer -> Tried to register a target that already exists.");
				return null;
			}



			NetworkTarget nt = new NetworkTarget(ep, playerSessionId, 0);

			nt.Team = team;
			nt.Character = "Hana";
			

			Client.RegisterTarget(nt);



			NetworkTarget[] targets = Targets.ToArray();

			for (int i = MIN_PLAYER_ID; i < 32; i++)
			{
				if (targets.FirstOrDefault(t => t.Id == (ushort)i) == default)
				{
					nt.Id = (ushort)i;

					TargetsShared[nt.RemoteEP] = nt;
					Targets.Add(nt);


					Log.WriteErrorLine("NetworkManager.RegisterTarget -> OnPlayerJoined");

					/*if (triggerEvent)
					{
						Game.Instance.RunOnMainThread(() =>
						{
							ServerArenaPhase.OnPlayerJoined(nt);
						});
					}*/

					return nt;
				}
			}
			

			return null;
		}

		public static bool RegisterTarget(IPEndPoint ep, string key, Game.Team team, string character, bool triggerEvent = false)
		{
			NetworkTarget nt = new NetworkTarget(ep, key, 0);

			nt.Team = team;
			nt.Character = character;

			if (TargetsShared.ContainsKey(nt.RemoteEP))
			{
				Log.WriteErrorLine("NetworkManager.RegisterTarget -> Tried to register a target that already exists.");
				return false;
			}

			Client.RegisterTarget(nt);



			NetworkTarget[] targets = Targets.ToArray();

			for (int i = MIN_PLAYER_ID; i < 32; i++)
			{
				if (targets.Where(t => t.Id == (ushort)i).FirstOrDefault() == default)
				{
					nt.Id = (ushort)i;

					TargetsShared[nt.RemoteEP] = nt;
					Targets.Add(nt);


					Log.WriteErrorLine("NetworkManager.RegisterTarget -> OnPlayerJoined");

					/*if (triggerEvent)
					{
						Game.Instance.RunOnMainThread(() =>
						{
							ServerArenaPhase.OnPlayerJoined(nt);
						});
					}*/

					return true;
				}
			}

			return false;

		}


		public static void RemoveTargetWithKey(string key)
		{
			var nt = Targets.Where(t => t.Key == key).FirstOrDefault();

			if (nt == null)
			{
				return;
			}

			Targets.Remove(nt);

			if (TargetsShared.ContainsKey(nt.RemoteEP))
			{
				NetworkTarget temp;
				while (!TargetsShared.TryRemove(nt.RemoteEP, out temp))
				{

				}
			}

			Client.RemoveTarget(nt);
		}


		public static void RemoveTarget(ushort id)
		{
			if (id < 1)
			{
				return;
			}

			Log.WriteLine("NetworkManager.RemoveTarget:a " + id);

			var nt = GetTargetWithId(id);

			Log.WriteLine("NetworkManager.RemoveTarget:b " + nt.Id);

			RemoveTarget(nt);
		}

		public static void RemoveTarget(NetworkTarget nt)
		{
			foreach (var e in Entities.Values.ToList())
			{
				if (e.GetOwnerId() == nt.Id)
				{
					Entities.Remove(e.GetEntityId());

					if ((e as NetworkBehaviour) != null)
					{
						Destroy((e as NetworkBehaviour).gameObject);
					}
				}
			}

			Log.WriteLine("NetworkManager.RemoveTarget:cb " + nt.Id);

			for (int i = instantiateHistory.Count - 1; i >= 0; i--)
			{
				if (instantiateHistory[i].Owner == nt.Id)
				{
					Log.WriteLine("NetworkManager.RemoveTarget:d " + nt.Id);
					instantiateHistory.RemoveAt(i);
				}
			}
			Log.WriteLine("NetworkManager.RemoveTarget:e " + nt.Id);

			NetworkTarget temp;
			while (TargetsShared.TryRemove(nt.RemoteEP, out temp) == false)
			{

			}

			Targets.Remove(nt);

			Client.RemoveTarget(nt);
		}

		public static NetworkTarget GetTargetWithEP(IPEndPoint ep)
		{
			return Targets.Where(t => t.RemoteEP.Equals(ep)).FirstOrDefault();
		}

		public static NetworkTarget GetTargetWithId(ushort id)
		{
			for (int i = 0; i < Targets.Count; i++)
			{
				if (Targets[i].Id == id)
				{
					return Targets[i];
				}
			}

			return null;
		}


		public static ushort GetAvailableEntityID()
		{
			for (int i = 1; i < 1000; i++)
			{
				if (Entities.ContainsKey((ushort)i) == false)
				{
					return (ushort)i;
				}

			}

			return 0;
		}


		public static void RegisterEntity(INetworkEntity entity, ushort id)
		{
			entity.SetEntityId(id);
			Entities.Add(id, entity);

		}

		public static void UnregisterEntity(INetworkEntity entity)
		{
			if (Entities.ContainsKey(entity.GetEntityId()))
			{
				Entities.Remove(entity.GetEntityId());
			}
		}

		public static INetworkEntity GetEntity(ushort id)
		{
			if (Entities.ContainsKey(id))
			{
				return Entities[id];
			}

			return null;
		}

		public static void SendInstantiateHistory(NetworkTarget nt)
		{
			foreach (var ins in NetworkManager.instantiateHistory)
			{
				var ns = BeginEvent(NetworkEventType.Instantiate);
				ins.WriteTo(ns);
				ns.reliability = NetworkStream.Reliability.Unreliable;
				nt.Send(ns);
			}
		}



		public static void OnGUI()
		{
			GUILayout.Label("My ID: " + LocalId);

			if (Client != null)
			{
				GUILayout.Label("REL: " + Client.reliablePackets.GetCount());
			}

			foreach (var nt in TargetsShared)
			{
				GUILayout.Label(nt.ToString());
			}

			foreach (var e in Entities.Values)
			{
				GUILayout.Label(e.GetOwnerId() + " -> " + e.GetEntityId());
			}

			foreach (var ins in instantiateHistory)
			{
				GUILayout.Label("InsH: " + ins.ToString());
			}
		}


		public static NetworkStream BeginEvent(NetworkEventType type)
		{
			NetworkStream ns = NetworkEvent.Create(type);

			return ns;
		}

		public static void RaiseEventNow(NetworkStream ns)
		{
			foreach (var nt in Targets.Where(t => t.SpawnTransform != null))
			{
				nt.Send(ns);
			}
		}

		public static void RaiseEvent(NetworkStream ns)
		{
			pendingEvents.Enqueue(ns);
		}

		public static void RaiseEvent(NetworkEventType type)
		{
			var ns = BeginEvent(type);
			RaiseEvent(ns);
		}

		public static GameObject Instantiate(InstantiateData data)
		{
			//GameObject prefab = Resources.Load<GameObject>(data.ResourcePath);
			GameObject g = ResourceManager.Instantiate(data.ResourcePath, data.Position, data.Rotation);
			g.transform.localScale = data.Scale;
			g.transform.position = data.Position;
			g.transform.rotation = data.Rotation;

			if (data.ResourcePath == "Characters/Ricket")
			{
				//Log.WriteErrorLine("ROTATION " + data.Rotation + "  -  " + g.transform.rotation);
			}

			NetworkBehaviour netEntity = g.GetComponent<NetworkBehaviour>();
			IGameEntity gameEntity = netEntity as IGameEntity;
			IExtraData extraReader = netEntity as IExtraData;

			if (extraReader != null && data.Extra != null)
			{
				extraReader.ApplyExtra(data.Extra);
			}


			//Log.WriteErrorLine("Instantiate: " + data.ResourcePath + "  " + netEntity);

			if (netEntity)
			{
				//Log.WriteErrorLine("Instantiate: " + "net");
				netEntity.OwnerId = data.Owner;
				netEntity.EntityId = data.Id;

				NetworkManager.RegisterEntity(g.GetComponent<NetworkBehaviour>(), data.Id);

				netEntity.OwnerId = data.Owner;

				netEntity.Owner = NetworkManager.GetTargetWithId(data.Owner);

			}


			if (gameEntity != null)
			{
				//Log.WriteErrorLine("GAME ENTITY");
				Game.Instance.RegisterGameEntity(gameEntity);
			}


			if (NetworkManager.IsServer)
			{
				var ns = BeginEvent(NetworkEventType.Instantiate);
				data.WriteTo(ns);


				//ns.reliability = NetworkStream.Reliability.Unreliable;
				RaiseEvent(ns);

				instantiateHistory.Add(data);

				Console.WriteLine("RAISED EVENT OF INSTANTIATE");
			}

			return g;
		}



		public static void Destroy(ushort entityId)
		{
			if (Entities.ContainsKey(entityId) == false)
			{
				return;
			}

			INetworkEntity entity = Entities[entityId];
			UnregisterEntity(entity);

			NetworkBehaviour beh = entity as NetworkBehaviour;

			if (beh != null)
			{
				Destroy(beh.gameObject);
			}
		}

		public static void Destroy(GameObject g)
		{
			if (Game.OfflineMode)
			{
				ResourceManager.ReleaseInstance(g);
				//GameObject.Destroy(g);
				return;
			}

			NetworkBehaviour networkBehaviour = g.GetComponent<NetworkBehaviour>();

			if (IsServer && networkBehaviour)
			{
				instantiateHistory = instantiateHistory.Where(d => d.Id != networkBehaviour.EntityId).ToList();

				var ns = BeginEvent(NetworkEventType.Destroy);
				ns.reliability = NetworkStream.Reliability.Reliable;
				ns.WriteUShort(networkBehaviour.EntityId);
				RaiseEvent(ns);
			}

			DestroyLocal(g);
		}

		private static void DestroyLocal(GameObject g)
		{
			NetworkBehaviour networkBehaviour = g.GetComponent<NetworkBehaviour>();
			IGameEntity gameEntity = networkBehaviour as IGameEntity;

			if (networkBehaviour)
			{
				UnregisterEntity(networkBehaviour);
			}

			if (gameEntity != null)
			{
				Game.Instance.UnregisterGameEntity(gameEntity);
			}

			//GameObject.Destroy(g);

			IDestroyController destroyController = networkBehaviour as IDestroyController;

			if (destroyController != null)
			{
				destroyController.OnDestroyed();
			}
			else
			{
				ResourceManager.ReleaseInstance(g);
			}

		}


		private static List<NetworkStream> frameEvents = new List<NetworkStream>();
		public static void Send()
		{
			if (Client == null)
			{
				return;
			}

			frameEvents.Clear();
			while (pendingEvents.Count > 0)
			{
				frameEvents.Add(pendingEvents.Dequeue());
			}

			foreach (var target in Targets)
			{
				foreach (var entity in Entities.Values)
				{
					NetworkStream ns = new NetworkStream(512); //streamPool.Get();
					ns.dataType = NetworkStream.DataType.Snapshot;
					ns.reliability = NetworkStream.Reliability.Unreliable;

					ns.WriteUShort(entity.GetEntityId());

					entity.OnSend(ns, target.Id);


					if (ns.Size < 3)
					{
						continue;
					}

					//Log.WriteErrorLine("SEND SNAP ");
					target.Send(ns);
				}

				if (IsClient)
				{
					NetworkStream cmdStream = new NetworkStream(512); //streamPool.Get(); //  
					InputManager.WriteCompressedCommands(cmdStream);
					InputManager.ClearCompressedCommands();
					target.Send(cmdStream);
				}
				else
				{

				}

				foreach (var es in frameEvents)
				{
					//Log.WriteErrorLine("SEND EVENT ");
					target.Send(es);
				}
			}

			foreach (var entity in Entities.Values)
			{
				entity.OnSendEnd();
			}
		}


		public static void Receive()
		{
			if (Client == null)
			{
				return;
			}

			//Log.WriteLine("NM _ rECEVE " + Targets.Count);

			if (OwnerlessPacketHandler != null)
			{
				NetworkStream ownerlessPacket;
				while (Client.ownerlessPackets.TryDequeue(out ownerlessPacket))
				{
					OwnerlessPacketHandler.OnOwnerlessPacketReceived(ownerlessPacket);
				}
			}

			foreach (var target in Targets)
			{
				//Log.WriteLine("NM _ rECEVE target ");
				NetworkStream ns;
				while ((ns = target.ReceiveSnapshot()) != null)
				{
					//Console.WriteLine("NM _ snapshot ");
					//Log.WriteLine("NM _ snapshot ");
					ushort entityId = ns.ReadUShort();

					if (!Entities.ContainsKey(entityId))
					{
						continue;
					}

					INetworkEntity entity = Entities[entityId];
					entity.OnReceive(ns);
				}

				if (EventHandler != null)
				{
					while ((ns = target.ReceiveEvent()) != null)
					{
						//Log.WriteErrorLine("NM _ event ");

						NetworkEventType eventType = (NetworkEventType)ns.ReadByte();

						//Log.WriteErrorLine("Ev " + eventType);

						if (localHandlers.ContainsKey(eventType))
						{
							localHandlers[eventType].Invoke(target, eventType, ns);
						}
						else
						{
							OnEvent_Other(target, eventType, ns);
						}
					}
				}

				if (IsServer)
				{
					target.GetCommandsReady();
				}
			}


		}

		private static void OnEvent_Instantiate(NetworkTarget target, NetworkEventType eventType, NetworkStream ns)
		{
			//Log.WriteErrorLine("OnEvent_Instantiate");

			InstantiateData data = new InstantiateData();
			data.ReadFrom(ns);
			Instantiate(data);
		}

		private static void OnEvent_Destroy(NetworkTarget target, NetworkEventType eventType, NetworkStream ns)
		{
			//Log.WriteErrorLine("OnEvent_Destroy");

			ushort entityId = ns.ReadUShort();
			if (!Entities.ContainsKey(entityId))
			{
				return;
			}

			NetworkBehaviour netBehaviour = Entities[entityId] as NetworkBehaviour;
			if (netBehaviour == null)
			{
				return;
			}

			DestroyLocal(netBehaviour.gameObject);
		}

		private static void OnEvent_None(NetworkTarget target, NetworkEventType eventType, NetworkStream ns)
		{
			Log.WriteErrorLine("NetworkManager.OnEvent -> Received event of type \"None\", from target " + target.ToString());
		}

		private static void OnEvent_Other(NetworkTarget target, NetworkEventType eventType, NetworkStream ns)
		{
			if (EventHandler == null)
			{
				//Log.WriteLine("NetworkManager.OnEvent -> Received event of type " + eventType + ", but there is no event handler.");
				return;
			}

			EventHandler.OnEventReceived(target, eventType, ns);
		}


		public static void Shutdown()
		{
			Log.WriteErrorLine("NetworkManager.Shutdown");

			Client?.Shutdown();
			Client = null;

			if (Entities != null)
			{
				foreach (var e in Entities.Values.ToList())
				{
					Entities.Remove(e.GetEntityId());

					if ((e as NetworkBehaviour) != null)
					{
						Destroy((e as NetworkBehaviour).gameObject);
					}
				}

				Entities.Clear();
			}

			Entities = new Dictionary<ushort, INetworkEntity>();

			IsConnected = false;
			LocalId = 0;


			//Game.Instance.ClearGameEntities();

			Targets?.Clear();
			Targets = new List<NetworkTarget>();

			TargetsShared?.Clear();
			TargetsShared = new ConcurrentDictionary<IPEndPoint, NetworkTarget>();

			//pendingEvents.Clear();
			pendingEvents = new Queue<NetworkStream>();

			instantiateHistory.Clear();
			instantiateHistory = new List<InstantiateData>();

			SetupLocalHandlers();

			EventHandler = null;
		}



	}

}
