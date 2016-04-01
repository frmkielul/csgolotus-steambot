using SteamKit2;
using SteamTrade;
using SteamTrade.TradeOffer;
using System;
using System.Collections.Generic;
using TradeAsset = SteamTrade.TradeOffer.TradeOffer.TradeStatusUser.TradeAsset;
using Quobject.SocketIoClientDotNet.Client;
using System.Web.Script.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace POSTData
{
    public class Item
    {
        // Steam ID to send trade offer to
        public string sid { get; set; }
        // market_hash_name of the item
        public string id { get; set; }
        // user's trade url for getting the token
        public string tradeurl { get; set; }
        // value in USD of the skins
        public float value { get; set; }
        // # of that item that they want
        public int amt { get; set; }
    }
}
namespace InventoryData
{
    public class Attribute
    {
        public UInt64 defindex { get; set; }
        //public int value { get; set; }
        public float float_value { get; set; }
    }

    public class Item
    {
        public Int64 id { get; set; }
        public Int64 original_id { get; set; }
        public Int64 defindex { get; set; }
        public Int64 level { get; set; }
        public Int64 quality { get; set; }
        public Int64 inventory { get; set; }
        public Int64 quantity { get; set; }
        public Int64 rarity { get; set; }
        public bool flag_cannot_trade { get; set; }
        public bool flag_cannot_craft { get; set; }
        public List<Attribute> attributes { get; set; }
    }

    public class Result
    {
        public int status { get; set; }
        public List<Item> items { get; set; }
    }

    public class RootObject
    {
        public Result result { get; set; }
    }
}

namespace SteamBot
{
    public class TradeOfferUserHandler : UserHandler
    {
        GenericInventory mySteamInventory;
        GenericInventory otherSteamInventory;
        JavaScriptSerializer js;

        public TradeOfferUserHandler(Bot bot, SteamID sid) : base(bot, sid)
        {
            mySteamInventory = otherSteamInventory = new GenericInventory(SteamWeb);
            js = new JavaScriptSerializer();
            Connect_Socket();
        }

        // Receiving a trade offer
        public override void OnNewTradeOffer(TradeOffer offer)
        {
            var myItems = offer.Items.GetMyItems();
            var theirItems = offer.Items.GetTheirItems();

            // Do not allow people to request items from the bot
            if (myItems.Count > 0)
            {
                offer.Decline();
                Log.Warn("Declined trade offer #" + offer.TradeOfferId + ". Reason: Requested an item from the bot.");
                SendChatMessage("Declined trade offer #" + offer.TradeOfferId + ". Reason: Requested an item from the bot.");
            }
            // Do not allow people to offer non-CS:GO items
            foreach (var x in theirItems)
            {
                if (x.AppId != 730)
                {
                    offer.Decline();
                    Log.Warn("Declined trade offer #" + offer.TradeOfferId + ". Reason: Non-CS:GO items offered.");
                    SendChatMessage("Declined trade offer #" + offer.TradeOfferId + ". Reason: Non-CS:GO items offered.");
                }
            }
            // All is well. Accept the trade.
            string tradeid;
            if (offer.Accept(out tradeid))
            {
                // potential issue: the webClient downloads the string after the transaction has been processed, so the assetid is no longer
                // existant in the json string. Or, the foreach loop is confusing me and im doing something wrong.
                InventoryData.RootObject json_items;
                using (var webClient = new System.Net.WebClient())
                {
                    string json = webClient.DownloadString("https://api.steampowered.com/IEconItems_730/GetPlayerItems/v1/?key=2457B1C97418CC3095E99484AF2DC660&steamid=" + Convert.ToInt64(offer.PartnerSteamId));
                    json_items = js.Deserialize<InventoryData.RootObject>(json);
                }

                List<Int64> original_ids = new List<Int64>() { Convert.ToInt64(offer.PartnerSteamId) };

                // compare offered items and their inventory data
                foreach (var x in theirItems)
                {
                    foreach (var y in json_items.result.items)
                    {
                        Console.WriteLine("inside ran");
                        break;
                    }
                    Console.WriteLine("Outside ran");
                }

                // Send the data to the Socket.io server
                string json_serialized = js.Serialize(original_ids);
                var socket = IO.Socket("http://localhost:8080");
                socket.Emit("response", json_serialized);

                Bot.AcceptAllMobileTradeConfirmations();
                Log.Success("Accepted trade offer successfully : Trade ID: " + tradeid);
            }
        }
        public void SendTradeOffer(ulong sid, List<string> items)
        {
            SteamID playerSID = new SteamID(sid);
            var offer = Bot.NewTradeOffer(playerSID);

            foreach (var x in items)
            {
                offer.Items.AddMyItem(730, 2, Convert.ToInt64(x), 1);
                Console.WriteLine("Added " + x + " to the offer to SteamID " + playerSID);
            }
            if (offer.Items.NewVersion)
            {
                string newOfferId;
                if (offer.Send(out newOfferId))
                {
                    Bot.AcceptAllMobileTradeConfirmations();
                    Log.Success("Trade offer sent : Offer ID " + newOfferId + " to SteamID " + playerSID);
                }
            }
        }
        public void Connect_Socket()
        {
            // Setup the connection to the server
            var socket = IO.Socket("http://localhost:8080");

            socket.On("gamedata", (data) =>
            {
                // First we must parse the JSON object 'data' and create a List
                string json = JsonConvert.SerializeObject(data);
                POSTData.Item[] json_items = js.Deserialize<POSTData.Item[]>(json);

                List<string> items = new List<string>();
                ulong steamid64 = Convert.ToUInt64(json_items[0].sid);

                foreach (var i in json_items) { items.Add(i.id); Console.WriteLine(i.id);  }
                items.RemoveAt(0);  // weird hack?
                SendTradeOffer(steamid64, items);
            });
        }
        public override void OnMessage(string message, EChatEntryType type) { }

        public override bool OnGroupAdd() { return false; }

        public override bool OnFriendAdd() { return IsAdmin; }

        public override void OnFriendRemove() { }

        public override void OnLoginCompleted() { }

        public override bool OnTradeRequest() { return false; }

        public override void OnTradeError(string error) { }

        public override void OnTradeTimeout() { }

        public override void OnTradeSuccess() { }

        public override void OnTradeAwaitingConfirmation(long tradeOfferID) { }

        public override void OnTradeInit() { }

        public override void OnTradeAddItem(Schema.Item schemaItem, Inventory.Item inventoryItem) { }

        public override void OnTradeRemoveItem(Schema.Item schemaItem, Inventory.Item inventoryItem) { }

        public override void OnTradeMessage(string message) { }

        public override void OnTradeReady(bool ready) { }

        public override void OnTradeAccept() { }
    }
}
