﻿using System;

namespace Rasa.Managers
{
    using Game;
    using Structures;

    public class CellManager
    {
        private static CellManager _instance;
        private static readonly object InstanceLock = new object();
        public const double CellSize = 25.0D;
        public const double CellBias = 32768.0D;
        public static uint CellPosX { get; set; }
        public static uint CellPosY { get; set; }
        public const uint CellViewRange = 2;   // view 2 cell's in every direction

        public static CellManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new CellManager();
                    }
                }

                return _instance;
            }
        }

        private CellManager()
        {
        }

        // Player
        public void AddToWorld(MapChannelClient client)
        {
            if (client.Player == null)
                return;

            var mapChannel = client.MapChannel;
            CellPosX = (uint) (client.Player.Actor.Position.PosX/CellSize + CellBias);
            CellPosY = (uint) (client.Player.Actor.Position.PosY/CellSize + CellBias);

            // calculate initial cell
            client.Player.Actor.CellLocation.CellPosX = CellPosX;
            client.Player.Actor.CellLocation.CellPosY = CellPosY;
            
            // get cell
            var mapCell = GetCell(mapChannel, CellPosX, CellPosY);
            if (mapCell != null)
            {
                // register player in cell
                mapCell.PlayerList.Add(client);
                // register notifications in visible area
                for (var ix = CellPosX - CellViewRange; ix <= CellPosX + CellViewRange; ix++)
                {
                    for (var iy = CellPosY - CellViewRange; iy <= CellPosY + CellViewRange; iy++)
                    {
                        var nMapCell = GetCell(mapChannel, ix, iy);
                        nMapCell?.PlayerNotifyList.Add(client);
                        // notify me about all objects that are visible to the cell
                        //if (nMapCell.ObjectList.Count > 0)
                        //{
                            // dynamicObject_cellIntroduceObjectsToClient(mapChannel, client, &nMapCell->ht_objectList[0], nMapCell->ht_objectList.size());
                        //}
                        // notify me about all creatures that are visible to the cell
                        //if (nMapCell.CreatureList.Count > 0)
                       // {
                            //creature_cellIntroduceCreaturesToClient(mapChannel, client, &nMapCell->ht_creatureList[0], nMapCell->ht_creatureList.size());
                       // }
                    }
                }
                if (mapCell.PlayerNotifyList.Count > 0)
                {
                    // notify all players of me
                    PlayerManager.Instance.CellIntroduceClientToPlayers(mapChannel, client, mapCell.PlayerNotifyList);
                    // notify me about all players that are visible here
                    PlayerManager.Instance.CellIntroducePlayersToClient(mapChannel, client, mapCell.PlayerNotifyList.Count);
                }
            }
        }
        
        public void DoWork(MapChannel mapChannel)
        {
            var currentTime = Environment.TickCount;
            if (mapChannel.MapCellInfo.TimeUpdateVisibility < currentTime)
            {
                UpdateVisibility(mapChannel);
                // update three times a second
                mapChannel.MapCellInfo.TimeUpdateVisibility = currentTime + 300;
            }
            // mob work

            // events etc...
        }

        public MapCell GetCell(MapChannel mapChannel, uint cellPosX, uint cellPosY)
        {
            var cellSeed = (cellPosX & 0xFFFF) | (cellPosY << 16);
            MapCell cell;
            if (mapChannel.MapCellInfo.Cells.TryGetValue(cellSeed, out cell))
            {
                cell.CellPosX = cellPosX;
                cell.CellPosY = cellPosY;
                return cell;
            }
            else
            {
                cell = new MapCell
                {
                    CellPosX = cellPosX,
                    CellPosY = cellPosY
                };
                // save cell
                mapChannel.MapCellInfo.LoadedCellList.Add(cell);
                mapChannel.MapCellInfo.LoadedCellCount++;
                // register cell
                mapChannel.MapCellInfo.Cells.Add(cellSeed, cell);   // ToDo check why it crash here sometimes
                return cell;
            }

        }

        public void RemoveFromWorld(Client client)
        {
            var actor = client.MapClient.Player.Actor;
            var mapChannel = client.MapClient.MapChannel;
            var player = client.MapClient.Player;
            
            if (client.MapClient.Player == null)
                return;
            
            var oldX1 = actor.CellLocation.CellPosX - CellViewRange;
            var oldX2 = actor.CellLocation.CellPosX + CellViewRange;
            var oldY1 = actor.CellLocation.CellPosY - CellViewRange;
            var oldY2 = actor.CellLocation.CellPosY + CellViewRange;
            for (var ix = oldX1; ix <=oldX2; ix++)
            {
                for (var iy = oldY1; iy <= oldY2; iy++)
                {
                    var nMapCell = GetCell(mapChannel, ix, iy);
                    if (nMapCell != null)
                    {
                        // remove notify entry
                        for (var i = 0; i < nMapCell.PlayerNotifyList.Count; i++)
                        {
                            if (nMapCell.PlayerNotifyList[i] == client.MapClient)
                            {
                                nMapCell.PlayerNotifyList.RemoveAt(i);
                                break;
                            }
                        }
                        // remove player visibility client-side
                        if (nMapCell.PlayerNotifyList.Count > 0)
                        {
                            PlayerManager.Instance.CellDiscardClientToPlayers(mapChannel, client.MapClient, nMapCell.PlayerNotifyList.Count);
                            PlayerManager.Instance.CellDiscardPlayersToClient(mapChannel, client.MapClient, nMapCell.PlayerNotifyList.Count);
                        }
                    }
                }
            }
            var mapCell = GetCell(mapChannel, actor.CellLocation.CellPosX, actor.CellLocation.CellPosY);
            if (mapCell != null)
            {
                var count = mapCell.PlayerNotifyList.Count;
                for (var i = 0; i < count; i++)
                {
                    if (mapCell.PlayerNotifyList[i] == client.MapClient)
                    {
                        mapCell.PlayerNotifyList.RemoveAt(i);
                        break;
                    }
                }
            }
        }
               
        public MapCell TryGetCell(MapChannel mapChannel, uint cellPosX, uint cellPosY)
        {
            var cellSeed = (cellPosX & 0xFFFF) | (cellPosY << 16);
            MapCell cell;
            if (mapChannel.MapCellInfo.Cells.TryGetValue(cellSeed, out cell))
            {
                cell.CellPosX = cellPosX;
                cell.CellPosY = cellPosY;
                return cell;
            }
            return null;
        }

        public void UpdateVisibility(MapChannel mapChannel)
        {
            for (var i = 0; i < mapChannel.PlayerCount; i++)
            {
                var client = mapChannel.PlayerList[i];
                if (client.Disconected || client.Player == null)
                    continue;

                var cellPosX = (uint)(client.Player.Actor.Position.PosX / CellSize + CellBias);
                var cellPosY = (uint)(client.Player.Actor.Position.PosY / CellSize + CellBias);
                if (client.Player.Actor.CellLocation.CellPosX != cellPosX ||
                    client.Player.Actor.CellLocation.CellPosY != cellPosY)
                {
                    var oldX1 = client.Player.Actor.CellLocation.CellPosX - CellViewRange;
                    var oldX2 = client.Player.Actor.CellLocation.CellPosX + CellViewRange;
                    var oldY1 = client.Player.Actor.CellLocation.CellPosY - CellViewRange;
                    var oldY2 = client.Player.Actor.CellLocation.CellPosY + CellViewRange;
                    // find players that leave visibility range
                    for (var ix = oldX1; ix <= oldX2; ix++)
                    {
                        for (var iy = oldY1; iy <= oldY2; iy++)
                        {
                            if ((ix >= (cellPosX - CellViewRange) && ix <= (cellPosX + CellViewRange)) && (iy >= (cellPosY - CellViewRange) && iy <= (cellPosY + CellViewRange)))
                                continue;
                            var nMapCell = GetCell(mapChannel, ix, iy);
                            if (nMapCell != null)
                            {
                                // remove notify entry
                                var count = nMapCell.PlayerNotifyList.Count;
                                for (var j = 0; j < count; j++)
                                {
                                    if (nMapCell.PlayerNotifyList[j] == client)
                                    {
                                        nMapCell.PlayerNotifyList.RemoveAt(j);
                                        break;
                                    }
                                }
                                // remove player visibility client-side
                                if (nMapCell.PlayerNotifyList.Count > 0)
                                {
                                    PlayerManager.Instance.CellDiscardClientToPlayers(mapChannel, client, nMapCell.PlayerNotifyList.Count);
                                    PlayerManager.Instance.CellDiscardPlayersToClient(mapChannel, client, nMapCell.PlayerNotifyList.Count);
                                }
                                // remove object visibility
                               // if (nMapCell->ht_objectList.empty() == false)
                               //     dynamicObject_cellDiscardObjectsToClient(mapChannel, client, &nMapCell->ht_objectList[0], nMapCell->ht_objectList.size());
                                // remove creature visibility
                               // if (nMapCell->ht_creatureList.empty() == false)
                               //     creature_cellDiscardCreaturesToClient(mapChannel, client, &nMapCell->ht_creatureList[0], nMapCell->ht_creatureList.size());
                            }
                        }
                    }
                    // find players that enter visibility range
                    for (var ix = cellPosX - CellViewRange; ix <= CellPosX + CellViewRange; ix++)
                    {
                        for(var iy = cellPosY - CellViewRange; iy <= cellPosY + CellViewRange; iy++)
                        {
                            if ((ix >= oldX1 && ix <= oldX2) && (iy >= oldY1 && iy <= oldY2))
                                continue;
                            var nnMapCell = GetCell(mapChannel, ix, iy);
                            if (nnMapCell != null)
                            {
                                // add player visibility client-side
                                if (nnMapCell.PlayerNotifyList.Count > 0)
                                {
                                    // notify all players of me
                                    PlayerManager.Instance.CellIntroduceClientToPlayers(mapChannel, client, nnMapCell.PlayerNotifyList);
                                    // notify me about all players that are visible here
                                    PlayerManager.Instance.CellIntroducePlayersToClient(mapChannel, client, nnMapCell.PlayerNotifyList.Count);
                                }
                                //if (nMapCell.ObjectList > 0)
                                //    dynamicObject_cellIntroduceObjectsToClient(mapChannel, client, &nMapCell->ht_objectList[0], nMapCell->ht_objectList.size());
                                // add creature visibility client-side
                                //if (nMapCell.CcreatureList > 0)
                                //    creature_cellIntroduceCreaturesToClient(mapChannel, client, &nMapCell->ht_creatureList[0], nMapCell->ht_creatureList.size());
                            }
                        }
                    }
                    // move the player entry
                    var mapCell = GetCell(mapChannel, client.Player.Actor.CellLocation.CellPosX, client.Player.Actor.CellLocation.CellPosX);
                    if (mapCell != null)
                    {
                        var count = mapCell.PlayerNotifyList.Count;
                        for (var k = 0; k < count; k++)
                        {
                            if (mapCell.PlayerNotifyList[k] == client)
                            {
                                mapCell.PlayerNotifyList.RemoveAt(k);
                                break;
                            }
                        }
                    }

                    // update location
                    client.Player.Actor.CellLocation.CellPosX = cellPosX;
                    client.Player.Actor.CellLocation.CellPosY = cellPosY;
                }
            }
        }
    }
}
