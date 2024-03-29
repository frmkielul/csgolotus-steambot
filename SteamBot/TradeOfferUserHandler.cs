﻿using SteamKit2;
using SteamTrade;
using SteamTrade.TradeOffer;
using System;
using System.Collections.Generic;
using Quobject.SocketIoClientDotNet.Client;
using System.Web.Script.Serialization;
using Newtonsoft.Json;
using System.Net;
using System.Text.RegularExpressions;

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
}

namespace SteamBot
{
    public static class SocketCall
    {
        public static bool HAS_CALLED = false;
    }
    public class ReceiptItem : AssetDescription
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }
    public class TradeOfferReceiptItems
    {
        public bool Success { get; set; }

        public List<ReceiptItem> ReceiptItems { get; set; }

        public TradeOfferReceiptItems()
        {
            ReceiptItems = new List<ReceiptItem>();
        }
    }
    public class TradeOfferUserHandler : UserHandler
    {
        GenericInventory mySteamInventory;
        GenericInventory otherSteamInventory;
        JavaScriptSerializer jsSerializer;
        Socket socket;

        public TradeOfferUserHandler(Bot bot, SteamID sid) : base(bot, sid)
        {
            mySteamInventory = otherSteamInventory = new GenericInventory(SteamWeb);
            //if (!SocketCall.HAS_CALLED)
            socket = IO.Socket("http://localhost:8080");
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
            string tradeid;
            try
            {
                if (offer.Accept(out tradeid))
                {
                    Console.WriteLine("Trade ID (not tradeofferid):" + tradeid);
                    var receiptItems = new TradeOfferReceiptItems();
                    var url = $"https://steamcommunity.com/trade/{tradeid}/receipt";
                    var resp = SteamWeb.Fetch(url, "GET", null, false);
                    var items = Regex.Matches(resp, @"oItem(?:[\s=]+)(?<jsonItem>[^;]*)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    List<long> newIds = new List<long>();
                    foreach (Match iM in items)
                    {
                        var g = iM.Groups["jsonItem"];
                        receiptItems.ReceiptItems.Add(JsonConvert.DeserializeObject<ReceiptItem>(g.Value));
                    }
                    foreach (var x in receiptItems.ReceiptItems)
                    {
                        newIds.Add(Convert.ToInt64(x.Id));
                    }
                    receiptItems.Success = true;
                    socket.Emit("response", JsonConvert.SerializeObject(new { sid = Convert.ToUInt64(offer.PartnerSteamId), items = newIds }));
                    Bot.AcceptAllMobileTradeConfirmations();
                }
            }
            catch(WebException e)
            {
                offer.Decline();
                SendChatMessage("Unfortunately, the Steam servers are down, and we cannot process your tradeoffer at this time. Please try again later. You can view the status of Steam servers at http://steamstat.us/");
            }
            
        }
        // Sending a trade offer
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
                try
                {
                    if (offer.Send(out newOfferId))
                    {
                        Bot.AcceptAllMobileTradeConfirmations();
                        Log.Success("Trade offer sent : Offer ID " + newOfferId + " to SteamID " + playerSID);
                    }
                }
                catch(WebException e)
                {
                    Console.WriteLine("Exception occured, oh no! " + e.Message);
                    SendChatMessage("Unfortunately, the Steam servers are down, and we cannot send you your requested items. Please try again later. You can view the status of Steam servers at http://steamstat.us/");
                    offer.Decline();
                }
            }
        }
        public void Connect_Socket()
        {
            // Prevent multiple sockets from opening. Hackey workaround, as always.
            if (SocketCall.HAS_CALLED) return;
            SocketCall.HAS_CALLED = true;

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