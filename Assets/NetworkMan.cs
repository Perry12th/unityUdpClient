using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using UnityEngine.PlayerLoop;

public class NetworkMan : MonoBehaviour
{
    public UdpClient udp;

    public GameObject playerPrefab;
    private List<GameObject> playerList;

    // List function to return a game object based on its id (returns null if failed to find it)
    GameObject findPlayerObject(string ID)
    {
        foreach(GameObject player in playerList)
        {
            if (player.GetComponent<PlayerCubeBehaviour>().ID == ID)
            {
                return player;
            }
        }
        return null;
    }

    // Start is called before the first frame update
    void Start()
    {
        udp = new UdpClient();

        playerList = new List<GameObject>();

        udp.Connect("localhost",12345);

        Byte[] sendBytes = Encoding.ASCII.GetBytes("connect");
      
        udp.Send(sendBytes, sendBytes.Length);

        udp.BeginReceive(new AsyncCallback(OnReceived), udp);

        InvokeRepeating("HeartBeat", 1, 1);
    }

    void OnDestroy(){
        udp.Dispose();
    }


    public enum commands{
        NEW_CLIENT,
        UPDATE,
        CLIENT_LEFT
    };
    
    [Serializable]
    public class Message{
        public commands cmd;
    }
    
    [Serializable]
    public class Player{
        public string id;
        [Serializable] 
        public struct receivedColor{
            public float R;
            public float G;
            public float B;
        }
        public receivedColor color;        
    }

    [Serializable]
    public class NewPlayer{
        public string id;
    }

    [Serializable]
    public class GameState{
        public Player[] players;
    }

    [Serializable]
    public class IO_Players
    {
        public NewPlayer[] players;
    }

    public Message latestMessage;
    public GameState lastestGameState;
    public IO_Players incomingPlayers;
    public IO_Players outgoingPlayers;

    void OnReceived(IAsyncResult result){
        // this is what had been passed into BeginReceive as the second parameter:
        UdpClient socket = result.AsyncState as UdpClient;
        
        // points towards whoever had sent the message:
        IPEndPoint source = new IPEndPoint(0, 0);

        // get the actual message and fill out the source:
        byte[] message = socket.EndReceive(result, ref source);
        
        // do what you'd like with `message` here:
        string returnData = Encoding.ASCII.GetString(message);
        Debug.Log("Got this: " + returnData);
        
        latestMessage = JsonUtility.FromJson<Message>(returnData);
        try{
            switch(latestMessage.cmd){
                case commands.NEW_CLIENT:
                    incomingPlayers = JsonUtility.FromJson<IO_Players>(returnData);
                    Debug.Log("New Players: " + incomingPlayers.players);
                    break;
                case commands.UPDATE:
                    lastestGameState = JsonUtility.FromJson<GameState>(returnData);
                    break;
                case commands.CLIENT_LEFT:
                    outgoingPlayers = JsonUtility.FromJson<IO_Players>(returnData);
                    Debug.Log("Players Left: " + outgoingPlayers.players);
                    break;
                default:
                    Debug.Log("Error");
                    break;
            }
        }
        catch (Exception e){
            Debug.Log(e.ToString());
        }
        
        // schedule the next receive operation once reading is done:
        socket.BeginReceive(new AsyncCallback(OnReceived), socket);
    }

    void SpawnPlayers(){

        if (incomingPlayers.players != null)
        {
            foreach (NewPlayer player in incomingPlayers.players)
            {
                // Get a new random spawn position for the cube
                Vector3 spawnPoint = new Vector3(UnityEngine.Random.Range(-5, 5),
                                                 UnityEngine.Random.Range(-5, 5),
                                                 UnityEngine.Random.Range(0, 5));

                // Create the newPlayer
                GameObject newPlayer = Instantiate(playerPrefab, spawnPoint, Quaternion.identity);
                // Set their ID tag
                newPlayer.GetComponent<PlayerCubeBehaviour>().ID = player.id;
                // Add the new player to the list
                playerList.Add(newPlayer);
                Debug.Log("playerList size: " + playerList.Count);

                incomingPlayers.players = null;
            }
            
        }
    }

    void UpdatePlayers(){
        foreach (Player player in lastestGameState.players)
        {
            // Get the player cube in the game
            GameObject playerCube = findPlayerObject(player.id);

            if (playerCube != null)
            {
                // Set its new colour
                playerCube.GetComponent<MeshRenderer>().material.color = new Color(player.color.R, player.color.G, player.color.B);
            }
        }
    }

    void DestroyPlayers(){
        if (outgoingPlayers.players != null)
        {
            foreach (NewPlayer player in outgoingPlayers.players)
            {
                // Get the player cube in the game
                GameObject playerCube = findPlayerObject(player.id);

                if (playerCube != null)
                {
                    // Remove object from the list
                    playerList.Remove(playerCube);

                    // Destory the actor
                    Destroy(playerCube);

                }
            }

            Array.Clear(outgoingPlayers.players, 0, outgoingPlayers.players.Length);

            outgoingPlayers.players = null;
        }
    }
    
    void HeartBeat(){
        Byte[] sendBytes = Encoding.ASCII.GetBytes("heartbeat");
        udp.Send(sendBytes, sendBytes.Length);
    }

    void Update()
    {
        SpawnPlayers();
        UpdatePlayers();
        DestroyPlayers();
    }
}