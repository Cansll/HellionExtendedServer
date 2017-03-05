﻿using System;
using System.Collections.Generic;
using ZeroGravity;
using ZeroGravity.Helpers;
using ZeroGravity.Math;
using ZeroGravity.Network;
using ZeroGravity.Objects;
using static ZeroGravity.Network.NetworkController;

namespace HellionExtendedServer.Controllers
{
    public class NetworkController
    {
        #region Fields

        private static NetworkController m_networkController;
        private ZeroGravity.Network.NetworkController m_network;

        #endregion Fields

        #region Properties

        public static NetworkController Instance { get { return m_networkController; } }
        public ThreadSafeDictionary<long, Client> ClientList { get { return m_network.clientList; } }

        #endregion Properties

        public NetworkController(ZeroGravity.Network.NetworkController networkController)
        {
            m_networkController = this;

            // Hook into the Chat Message event and add in ours along side the original
            networkController.EventSystem.AddListener(typeof(TextChatMessage), new EventSystem.NetworkDataDelegate(this.TextChatMessageListener));
            Console.WriteLine("Chat Message Listener Added.");

            // Hook into the player spawn event and add in ours as well!
            networkController.EventSystem.AddListener(typeof(PlayerSpawnRequest), new EventSystem.NetworkDataDelegate(this.PlayerSpawnRequestListener));
            Console.WriteLine("Player Spawns Listener Added.");

            // [IN TEST] Could be used to detect when the player is physicly in the server
            networkController.EventSystem.AddListener(typeof(ZeroGravity.Network.PlayersOnServerRequest), new EventSystem.NetworkDataDelegate(this.PlayerOnServerListener));
            Console.WriteLine("Player On Server Listener Added.");

            m_network = networkController;
            Console.WriteLine("Network Controller Loaded!");
        }

        #region Event Handlers

        /// <summary>
        /// This method is invoked when the event is fired
        /// </summary>
        /// <param name="data"> the network data object</param>
        private void PlayerOnServerListener(NetworkData data)
        {
            try
            {
                PlayersOnServerRequest playerOnServerRequest = data as PlayersOnServerRequest;

                if (playerOnServerRequest == null)
                    return;

                Player ply;
                if (m_network.clientList.ContainsKey(playerOnServerRequest.Sender))
                    ply = m_network.clientList[playerOnServerRequest.Sender].Player;
                else
                    return;

                MessageAllClients(string.Format("Be welcome {0} on {1} !", playerOnServerRequest.Sender, Server.Instance.ServerName));

            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Hellion Extended Server[SpawnRequest]:" + ex.InnerException.ToString());
            }
        }

        /// <summary>
        /// This method is invoked when the event is fired
        /// </summary>
        /// <param name="data"> the network data object</param>
        private void PlayerSpawnRequestListener(NetworkData data)
        {
            try
            {
                PlayerSpawnRequest playerSpawnRequest = data as PlayerSpawnRequest;

                if (playerSpawnRequest == null)
                    return;

                Player ply;
                if (m_network.clientList.ContainsKey(playerSpawnRequest.Sender))
                    ply = m_network.clientList[playerSpawnRequest.Sender].Player;
                else
                    return;

                Console.WriteLine(ply.Name + " spawned (" + ply.SteamId + ") ");
                MessageAllClients(ply.Name + " has spawned!", false, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Hellion Extended Server[SpawnRequest]:" + ex.InnerException.ToString());
            }
        }

        /// <summary>
        /// This method is invoked when the event is fired
        /// </summary>
        /// <param name="data"> the network data object</param>
        private void TextChatMessageListener(NetworkData data)
        {
            try
            {
                TextChatMessage textChatMessage = data as TextChatMessage;

                Console.WriteLine("(" + textChatMessage.Sender + ")" + textChatMessage.Name + ": " + textChatMessage.MessageText);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Hellion Extended Server[Chat]:" + ex.InnerException.ToString());
            }
        }

        #endregion Event Handlers

        #region Methods

        /// <summary>
        ///
        /// </summary>
        /// <param name="msg">the message to send</param>
        /// <param name="sendAsServer"> sends Server as the name of the message</param>
        /// <param name="printToConsole"> prints the message to the console</param>
        public void MessageAllClients(string msg, bool sendAsServer = true, bool printToConsole = true)
        {
            if (String.IsNullOrEmpty(msg))
                return;

            byte[] guid = Guid.NewGuid().ToByteArray();

            TextChatMessage textChatMessage = new TextChatMessage();

            textChatMessage.GUID = BitConverter.ToInt64(guid, 0);
            textChatMessage.Name = (sendAsServer ? "Server :" : "");
            textChatMessage.MessageText = msg;
            m_network.SendToAllClients((NetworkData)textChatMessage, textChatMessage.Sender);

            if (printToConsole)
                Console.WriteLine(textChatMessage.Name + "" + msg);
        }

        /// <summary>
        /// This method allow to send a message to a specific player
        /// </summary>
        /// <param name="msg">Message to send</param>
        /// <param name="SenderName">Name of the sender (Server for exemple)</param>
        /// <param name="ReceiverName">Name or steamid of the receiver</param>
        /// <param name="steamId">True if the receiverName is the steamID</param>
        /// <returns></returns>
        public void MessageToClient(string msg, string SenderName, string ReceiverName, bool steamId = false)
        {
            if (String.IsNullOrEmpty(msg))
                return;

            byte[] guid = Guid.NewGuid().ToByteArray();

            TextChatMessage textChatMessage = new TextChatMessage();

            textChatMessage.GUID = BitConverter.ToInt64(guid, 0);
            textChatMessage.Name = (SenderName);
            textChatMessage.MessageText = msg;

            Player receiver = FindPlayer(ReceiverName, steamId);
            if (receiver != null)
            {
                Client recClient = GetClient(receiver);
                if (receiver != null)
                {
                    m_network.SendToGameClient(recClient.ClientGUID, (NetworkData)textChatMessage);
                    Console.WriteLine(textChatMessage.Name + "->" + ReceiverName + ": " + msg);
                }
                else
                {
                    Console.WriteLine("This player is not connected.");
                }
            }
            else
            {
                Console.WriteLine("This player don't exist.");
            }
        }

        /// <summary>
        /// This method allow to get a specific player with his ingame name or steamID
        /// </summary>
        /// <param name="name">Name or steamID (steamId=true requiered)</param>
        /// <param name="steamId">True if name is the steamID</param>
        /// <returns></returns>
        public Player FindPlayer(string name, bool steamId = false)
        {
            if (name == "")
                return null;

            Dictionary<long, Player>.ValueCollection players = HES.CurrentServer.AllPlayers;

            foreach (var player in players)
                if (player.Name == name)
                    return player;

            return null;
        }

        /// <summary>
        /// This method allow to get the client of a specific player (usefull to get his ClientGUID
        /// </summary>
        /// <param name="player">Researched player</param>
        /// <returns></returns>
        public Client GetClient(Player player)
        {
            if (player == null)
                return null;

            ThreadSafeDictionary<long, Client> clients = m_network.clientList;
            foreach (var client in clients)
            {
                if (client.Value.Player == player)
                    return client.Value;
            }

            return null;
        }

        #endregion Methods
    }
}