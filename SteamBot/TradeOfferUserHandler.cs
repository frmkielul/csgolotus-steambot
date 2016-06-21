using SteamKit2;
using SteamTrade;
using SteamTrade.TradeOffer;
using System;
using System.Collections.Generic;
using Quobject.SocketIoClientDotNet.Client;
using System.Web.Script.Serialization;
using Newtonsoft.Json;
using System.Net;

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
    public class TradeReq
    {
        public string sid { get; set; }
        public string tradeID { get; set; }
        public List<long> items { get; set; }
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
        JavaScriptSerializer jsSerializer;

        public TradeOfferUserHandler(Bot bot, SteamID sid) : base(bot, sid)
        {
            mySteamInventory = otherSteamInventory = new GenericInventory(SteamWeb);
            jsSerializer = new JavaScriptSerializer();
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
            List<long> asset_ids = new List<long>() {};
            string tradeID = offer.TradeOfferId;
            foreach (var x in theirItems) asset_ids.Add(x.AssetId);
            var socket = IO.Socket("http://localhost:8080");
            socket.Emit("response", JsonConvert.SerializeObject(new { sid = Convert.ToUInt64(offer.PartnerSteamId), tradeID = tradeID, items = asset_ids }));   
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
                POSTData.Item[] json_items = jsSerializer.Deserialize<POSTData.Item[]>(json);

                List<string> items = new List<string>();
                ulong steamid64 = Convert.ToUInt64(json_items[0].sid);

                foreach (var i in json_items) { items.Add(i.id); Console.WriteLine(i.id);  }

                items.RemoveAt(0);  // weird hack.
                SendTradeOffer(steamid64, items);
            });
            socket.On("sendtrade", (data) =>
            {
                Console.WriteLine("SENDTRADE REQ RECEIVED! Attempting to accept trade offer. id #" + data);
                TradeOffer t;
                this.Bot.tradeOfferManager.GetOffer((String) data, out t);  // out keyword means that it's passed by reference like the & in C++
                if (!(t.OfferState == TradeOfferState.TradeOfferStateAccepted)) t.Accept();
                Bot.AcceptAllMobileTradeConfirmations();
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
