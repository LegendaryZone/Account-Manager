﻿using Google.Protobuf;
using POGOProtos.Data;
using POGOProtos.Inventory;
using POGOProtos.Inventory.Item;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Requests.Messages;
using POGOProtos.Networking.Responses;
using PokemonGoGUI.Extensions;
using PokemonGoGUI.GoManager.Models;
using PokemonGoGUI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PokemonGoGUI.GoManager
{
    public partial class Manager
    {
        public MethodResult<List<InventoryItem>> UpdateInventory()
        {
            try
            {

                try
                {
                    UpdatePlayerStats();
                    UpdatePokemon();
                    UpdatePokedex();
                    UpdatePokemonCandy();
                    UpdateItemList();
                    UpdateIncubators();
                    InventoryUpdateCaller(EventArgs.Empty);

                    return new MethodResult<List<InventoryItem>>
                    {
                        Message = "Successfully grabbed inventory items",
                        Success = true
                    };
                }
                catch (Exception)
                {
                    return new MethodResult<List<InventoryItem>>
                    {
                        Message = "Failed to get inventory",
                        Success = false
                    };
                }
            }
            catch (Exception)
            {
                return new MethodResult<List<InventoryItem>>
                {
                    Message = "Failed to get inventory",
                    Success = false
                };
            }
        }

        public void UpdateItemList()
        {
                Items = _client.ClientSession.Player.Inventory.InventoryItems.Where(x => x.InventoryItemData.Item != null).Select(x => x.InventoryItemData.Item).ToList();

        }

        public void UpdatePlayerStats()
        {
                InventoryItem item = _client.ClientSession.Player.Inventory.InventoryItems.FirstOrDefault(
                    x => x.InventoryItemData.PlayerStats != null);

                Stats = item.InventoryItemData.PlayerStats;
        }


        public void UpdatePokemon()
        {
            Pokemon = _client.ClientSession.Player.Inventory.InventoryItems.Where(x => x.InventoryItemData.PokemonData != null && !x.InventoryItemData.PokemonData.IsEgg).Select(x => x.InventoryItemData.PokemonData).ToList();
            Eggs = _client.ClientSession.Player.Inventory.InventoryItems.Where(x => x.InventoryItemData.PokemonData != null && x.InventoryItemData.PokemonData.IsEgg).Select(x => x.InventoryItemData.PokemonData).ToList();
        }

        public void UpdatePokedex()
        {
            Pokedex = _client.ClientSession.Player.Inventory.InventoryItems.Where(x => x.InventoryItemData.PokedexEntry != null).Select(x => x.InventoryItemData.PokedexEntry).ToList();
        }

        public void UpdateIncubators()
        {
            Incubators = _client.ClientSession.Player.Inventory.InventoryItems.First(x => x.InventoryItemData.EggIncubators != null).InventoryItemData.EggIncubators.EggIncubator.ToList();
        }

        public void UpdatePokemonCandy()
        {
                PokemonCandy = _client.ClientSession.Player.Inventory.InventoryItems.Where(x => x.InventoryItemData.Candy != null).Select(x => x.InventoryItemData.Candy).ToList();
        }

        public async Task<MethodResult> RecycleFilteredItems()
        {
            if (!UserSettings.RecycleItems)
            {
                return new MethodResult
                {
                    Message = "Item deletion not enabled"
                };
            }

            MethodResult<List<InventoryItem>> inventoryResponse = UpdateInventory();

            if (!inventoryResponse.Success)
            {
                return inventoryResponse;
            }


            UpdateItemList();

            foreach (ItemData item in Items)
            {
                InventoryItemSetting itemSetting = UserSettings.ItemSettings.FirstOrDefault(x => x.Id == item.ItemId);

                if (itemSetting == null)
                {
                    continue;
                }

                int toDelete = item.Count - itemSetting.MaxInventory;

                if (toDelete <= 0)
                {
                    continue;
                }

                await RecycleItem(itemSetting, toDelete);

                await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));
            }


            return new MethodResult
            {
                Message = "Success",
                Success = true
            };
        }

        public async Task<MethodResult> RecycleItem(ItemData item, int toDelete)
        {
            InventoryItemSetting itemSetting = UserSettings.ItemSettings.FirstOrDefault(x => x.Id == item.ItemId);

            if(itemSetting == null)
            {
                return new MethodResult();
            }

            return await RecycleItem(itemSetting, toDelete);
        }

        public async Task<MethodResult> RecycleItem(InventoryItemSetting itemSetting, int toDelete)
        {
            //TODO: revise
            //return new MethodResult { Message = "Dev mode sorry" };
            try
            {
                var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
                {
                    RequestType = RequestType.RecycleInventoryItem,
                    RequestMessage = new RecycleInventoryItemMessage
                    {
                        Count = toDelete,
                        ItemId = itemSetting.Id
                    }.ToByteString()
                });

                RecycleInventoryItemResponse recycleInventoryItemResponse = null;

                recycleInventoryItemResponse = RecycleInventoryItemResponse.Parser.ParseFrom(response);
                LogCaller(new LoggerEventArgs(String.Format("Deleted {0} {1}. Remaining {2}", toDelete, itemSetting.FriendlyName, recycleInventoryItemResponse.NewCount), LoggerTypes.Recycle));

                return new MethodResult
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                LogCaller(new LoggerEventArgs(String.Format("Failed to recycle iventory item {0}", itemSetting.FriendlyName), LoggerTypes.Warning, ex));

                return new MethodResult();
            }
        }

        public double FilledInventorySpace()
        {
            if(Items == null || PlayerData == null)
            {
                return 100;
            }

            return (double)Items.Sum(x => x.Count) / PlayerData.MaxItemStorage * 100;
        }
    }
}
