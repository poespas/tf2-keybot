using SteamKit2;
using SteamTrade;
using SteamTrade.TradeOffer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Json;

namespace SteamBot
{
    public class TradeOfferUserHandler : UserHandler
    {
        
        public static System.Timers.Timer aTimer;
        static BackgroundWorker _bw = new BackgroundWorker();

        private readonly GenericInventory mySteamInventory;
        private readonly GenericInventory OtherSteamInventory;
        public TradeOfferUserHandler(Bot bot, SteamID sid)
            : base(bot, sid)
        {
            mySteamInventory = new GenericInventory(SteamWeb);
            OtherSteamInventory = new GenericInventory(SteamWeb);
        }

        public int buyPrice = 0; // These values are your price
        public int sellPrice = 999; //They were automated priced in the past with PHP and databases, but I removed that in this case.

        SteamID ownerID = new SteamID(76561198043436466);

        List<ulong> UsedAssetIDs = new List<ulong>();

        public override void OnTradeOfferUpdated(TradeOffer offer)
        {
            if (offer.OfferState == TradeOfferState.TradeOfferStateActive && !offer.IsOurOffer)
            {
                OnNewTradeOffer(offer);
            }
        }

        private void OnNewTradeOffer(TradeOffer offer)
        {
            var myItems = offer.Items.GetMyItems();
            var theirItems = offer.Items.GetTheirItems();
            if (myItems.Count == 0)
            {
                offer.Accept();
                Bot.SteamFriends.SendChatMessage(ownerID, EChatEntryType.ChatMsg, "I received a donation offer!");
                Bot.AcceptAllMobileTradeConfirmations();
                Log.Success("Received a donation-offer");
            }
            else
            {

                Bot.TradeOfferEscrowDuration CurrentEscrow = Bot.GetEscrowDuration(offer.TradeOfferId);

                //Check if trader has delayed trades
                if (CurrentEscrow.DaysTheirEscrow > 2)
                {
                    Log.Error("Trade offer has been declined due to escrow.");
                    Bot.SteamFriends.SendChatMessage(ownerID, EChatEntryType.ChatMsg, "Incoming trade offer has been declined due to escrow.");
                    offer.Decline();
                }
                else
                {
                    List<long> contextId = new List<long>();
                    contextId.Add(2);
                    contextId.Add(6);

                    mySteamInventory.load(440, contextId, Bot.SteamClient.SteamID);
                    OtherSteamInventory.load(440, contextId, offer.PartnerSteamId);

                    int MyRef = 0;
                    int MyKey = 0;

                    int TheirRef = 0;
                    int TheirKey = 0;

                    #region User
                    for (int count = 0; count < theirItems.Count; count++)
                    {
                        if (theirItems[count].AppId == 440)
                        {
                            if (OtherSteamInventory.getDescription((ulong)theirItems[count].AssetId).name == "Scrap Metal")
                            {
                                TheirRef += 1;
                            }
                            else if (OtherSteamInventory.getDescription((ulong)theirItems[count].AssetId).name == "Reclaimed Metal")
                            {
                                TheirRef += 3;
                            }
                            else if (OtherSteamInventory.getDescription((ulong)theirItems[count].AssetId).name == "Refined Metal")
                            {
                                TheirRef += 9;
                            }
                            else if (OtherSteamInventory.getDescription((ulong)theirItems[count].AssetId).name == "Mann Co. Supply Crate Key")
                            {
                                TheirKey++;
                            }
                        }
                    }
                    #endregion
                    #region Bot
                    for (int count = 0; count < myItems.Count; count++)
                    {
                        if (mySteamInventory.getDescription((ulong)myItems[count].AssetId).name == "Scrap Metal")
                        {
                            MyRef += 1;
                        }
                        else if (mySteamInventory.getDescription((ulong)myItems[count].AssetId).name == "Reclaimed Metal")
                        {
                            MyRef += 3;
                        }
                        else if (mySteamInventory.getDescription((ulong)myItems[count].AssetId).name == "Refined Metal")
                        {
                            MyRef += 9;
                        }
                        else if (mySteamInventory.getDescription((ulong)myItems[count].AssetId).name == "Mann Co. Supply Crate Key")
                        {
                            MyRef += 9;
                        }
                    }
                    #endregion
                    #region Calculate
                    Console.Write(" - TheirKey: " + TheirKey);
                    Console.Write(" - Buyprice: " + buyPrice);
                    Console.WriteLine(" - MyRef: " + MyRef);
                    Console.Write(" - MyKey: " + MyKey);
                    Console.Write(" - SellPrice: " + sellPrice);
                    Console.WriteLine(" - TheirRef: " + TheirRef);
                    if (((TheirKey * buyPrice) == MyRef) && ((MyKey * sellPrice) == TheirRef))
                    {
                        Log.Success("[#" + offer.TradeOfferId + "] Accepted Offer.");
                        offer.Accept();
                        Bot.AcceptAllMobileTradeConfirmations();
                        Bot.SteamFriends.SendChatMessage(ownerID, EChatEntryType.ChatMsg, "I've done a succesful offer with " + Bot.SteamFriends.GetFriendPersonaName(offer.PartnerSteamId) + ".");
                    }
                    else if (IsAdmin)
                    {
                        string tradeid;
                        offer.Accept(out tradeid);
                        Log.Success("[ADMINOFFER] Accepted trade offer successfully : Trade ID: " + tradeid);
                        Bot.AcceptAllMobileTradeConfirmations();
                    }
                    else
                    {
                        Log.Success("[#" + offer.TradeOfferId + "] Declined Offer.");
                        offer.Decline();
                        Bot.AcceptAllMobileTradeConfirmations();
                    }
                    #endregion
                }
            }

        }

        public override void OnMessage(string message, EChatEntryType type)
        {
            message = message.ToLower();
            Log.Success(Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " : " + message);
            Bot.SteamFriends.SendChatMessage(ownerID, EChatEntryType.ChatMsg, Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " : " + message);
            #region Buy
            if (message.StartsWith("!buy"))
            {
                if (message == "!buy")
                {
                    SendChatMessage("You have to put a amount in. Ex: !buy 2 for buying 2 keys.");
                }
                else
                {
                    bool notEnough = false;
                    SendChatMessage("Your request will be dealt with shortly.");
                    int amount = 0;

                    if (message.Substring(6) == "x")
                    {
                        amount = 0;
                        SendChatMessage("You didn't enter a number. Ex: !buy 2 for buying 2 keys.");
                    }
                    else
                    {
                        try
                        {
                            amount = int.Parse(message.Substring(5));
                        }
                        catch
                        {
                            SendChatMessage("You didn't put a valid number in. Example: !buy 2 for buying 2 keys");
                        }
                    }
                    // Check if the user has escrow.
                    if (Bot.GetEscrowDuration(new SteamID(OtherSID), "").DaysTheirEscrow > 2)
                    {
                        SendChatMessage("Sorry, You require to have an account with a valid Phone Authenticator.");
                    }
                    else
                    {
                        // Bot adds keys. User adds metal.

                        List<long> contextId = new List<long>();
                        contextId.Add(2);
                        contextId.Add(6);

                        mySteamInventory.load(440, contextId, Bot.SteamClient.SteamID);
                        OtherSteamInventory.load(440, contextId, OtherSID);

                        #region User-part
                        //Checking users stock.
                        int UserRefCount = 0;
                        int UserRecCount = 0;
                        int UserScrapCount = 0;
                        int KeysInStock = 0;
                        foreach (GenericInventory.Item item in OtherSteamInventory.items.Values)
                        {
                            if (OtherSteamInventory.getDescription(item.assetid).name == "Refined Metal" && OtherSteamInventory.getDescription(item.assetid).tradable)
                            {
                                UserRefCount++;
                            }
                            else if (OtherSteamInventory.getDescription(item.assetid).name == "Reclaimed Metal" && OtherSteamInventory.getDescription(item.assetid).tradable)
                            {
                                UserRecCount++;
                            }
                            else if (OtherSteamInventory.getDescription(item.assetid).name == "Scrap Metal" && OtherSteamInventory.getDescription(item.assetid).tradable)
                            {
                                UserScrapCount++;
                            }
                        }

                        int MetalRequired = amount * sellPrice;
                        int OfferChangeRequired = 0;
                        decimal RefRequired = Math.Floor((decimal)MetalRequired / 9);
                        if (UserRefCount < RefRequired)
                        {
                            RefRequired = UserRefCount;
                        }
                        decimal metalRequiredR = MetalRequired - (RefRequired * 9);
                        decimal RecRequired = Math.Floor((decimal)metalRequiredR / 3);
                        if (UserRecCount < RecRequired)
                        {
                            RecRequired = UserRecCount;
                        }
                        decimal ScrapRequired = metalRequiredR - (RecRequired * 3);
                        if (UserScrapCount < ScrapRequired)
                        {
                            while (UserScrapCount < ScrapRequired)
                            {
                                RecRequired++;
                                ScrapRequired -= 3;
                                if (ScrapRequired < 0)
                                {
                                    OfferChangeRequired = (int)ScrapRequired * -1;
                                    ScrapRequired = 0;
                                }
                            }
                        }
                        if (UserRecCount < RecRequired)
                        {
                            while (UserRecCount < RecRequired)
                            {
                                if (UserRefCount > RefRequired)
                                {
                                    RefRequired++;
                                    RecRequired -= 3;
                                    if (RecRequired < 0)
                                    {
                                        OfferChangeRequired = ((int)RecRequired * -1) * 3;
                                        RecRequired = 0;
                                    }
                                }
                                else
                                {
                                    ScrapRequired += 3;
                                    RecRequired--;
                                }
                            }
                        }

                        var offer = Bot.NewTradeOffer(OtherSID);

                        foreach (GenericInventory.Item item in OtherSteamInventory.items.Values)
                        {
                            if (OtherSteamInventory.getDescription(item.assetid).name == "Refined Metal" && OtherSteamInventory.getDescription(item.assetid).tradable)
                            {
                                if (RefRequired > 0)
                                {
                                    offer.Items.AddTheirItem(item.appid, item.contextid, (long)item.assetid);
                                    RefRequired--;
                                }
                            }
                            else if (OtherSteamInventory.getDescription(item.assetid).name == "Reclaimed Metal" && OtherSteamInventory.getDescription(item.assetid).tradable)
                            {
                                if (RecRequired > 0)
                                {
                                    offer.Items.AddTheirItem(item.appid, item.contextid, (long)item.assetid);
                                    RecRequired--;
                                }
                            }
                            else if (OtherSteamInventory.getDescription(item.assetid).name == "Scrap Metal" && OtherSteamInventory.getDescription(item.assetid).tradable)
                            {
                                if (ScrapRequired > 0)
                                {
                                    offer.Items.AddTheirItem(item.appid, item.contextid, (long)item.assetid);
                                    ScrapRequired--;
                                }
                            }
                            if (RefRequired == 0 && RecRequired == 0 && ScrapRequired == 0)
                            {
                                break;
                            }
                        }
                        if (!(RefRequired == 0 && RecRequired == 0 && ScrapRequired == 0))
                        {
                            notEnough = true;
                            SendChatMessage("Sorry, You dont have enough metal. (You need " + string.Format("{0:0.000}", (int.Parse("" + ScrapRequired) / 9.0)).Substring(0, 4) + " Scrap more.)");
                        }
                        foreach (GenericInventory.Item item in mySteamInventory.items.Values)
                        {
                            if (mySteamInventory.getDescription(item.assetid).name == "Mann Co. Supply Crate Key" && mySteamInventory.getDescription(item.assetid).tradable)
                            {
                                KeysInStock++;
                            }
                        }
                        if (amount > KeysInStock)
                        {
                            SendChatMessage("Sorry, I dont have enough keys in stock. (Current stock: " + KeysInStock + " keys).");
                            notEnough = true;
                        }
                        #endregion
                        #region BotPart
                        if (!notEnough)
                        {
                            int KeysRemaining = amount;
                            foreach (GenericInventory.Item item in mySteamInventory.items.Values)
                            {
                                if (KeysRemaining > 0)
                                {
                                    if (mySteamInventory.getDescription(item.assetid).name == "Mann Co. Supply Crate Key" && mySteamInventory.getDescription(item.assetid).tradable && !UsedAssetIDs.Contains(item.assetid))
                                    {
                                        offer.Items.AddMyItem(item.appid, item.contextid, (long)item.assetid);
                                        KeysRemaining--;
                                    }
                                }
                                if (OfferChangeRequired >= 9)
                                {
                                    if (mySteamInventory.getDescription(item.assetid).name == "Refined Metal" && mySteamInventory.getDescription(item.assetid).tradable && !UsedAssetIDs.Contains(item.assetid))
                                    {
                                        offer.Items.AddMyItem(item.appid, item.contextid, (long)item.assetid);
                                        OfferChangeRequired -= 9;
                                    }
                                }
                            }
                            foreach (GenericInventory.Item item in mySteamInventory.items.Values)
                            {
                                if (OfferChangeRequired >= 3 && OfferChangeRequired < 9)
                                {
                                    if (mySteamInventory.getDescription(item.assetid).name == "Reclaimed Metal" && mySteamInventory.getDescription(item.assetid).tradable && !UsedAssetIDs.Contains(item.assetid))
                                    {
                                        offer.Items.AddMyItem(item.appid, item.contextid, (long)item.assetid);
                                        OfferChangeRequired -= 3;
                                    }
                                }
                            }

                            foreach (GenericInventory.Item item in mySteamInventory.items.Values)
                            {
                                if (OfferChangeRequired >= 1 && OfferChangeRequired < 3)
                                {
                                    if (mySteamInventory.getDescription(item.assetid).name == "Scrap Metal" && mySteamInventory.getDescription(item.assetid).tradable && !UsedAssetIDs.Contains(item.assetid))
                                    {
                                        offer.Items.AddMyItem(item.appid, item.contextid, (long)item.assetid);
                                        OfferChangeRequired -= 1;
                                    }
                                }
                            }
                            if (KeysRemaining != 0 && OfferChangeRequired != 0)
                            {
                                SendChatMessage("Sorry, I dont have enough stock to send this order.");
                            }

                            #endregion
                            #region Sending Offer..
                            else
                            {
                                if (offer.Items.NewVersion)
                                {
                                    string newOfferId;
                                    if (offer.Send(out newOfferId, "Please leave a +rep if liked it!"))
                                    {
                                        Bot.AcceptAllMobileTradeConfirmations();
                                        SendChatMessage("Thanks for trading with us.Your order (" + amount + " keys) has been sent.");
                                        //SendChatMessage("If you like a comment from me, Send me \"+rep\" in the chat.");
                                        SendChatMessage("You can accept it here: https://steamcommunity.com/tradeoffer/" + newOfferId + "/");
                                        Log.Success("Trade offer sent : Offer ID " + newOfferId);
                                        Bot.SteamFriends.SendChatMessage(ownerID, EChatEntryType.ChatMsg, "I've sold a key to " + Bot.SteamFriends.GetFriendPersonaName(OtherSID));
                                    }
                                }
                            }
                        }
                        #endregion
                    }
                }
            }
            #endregion
            #region Sell
            else if (message.StartsWith("!sell"))
            {
                if (message == "!sell")
                {
                    SendChatMessage("You have to put a amount in. Ex: !sell 2 for selling 2 keys.");
                }
                else
                {
                    bool notEnough = false;
                    SendChatMessage("Your request will be dealt with shortly.");
                    int amount = 0;
                    if (message.Substring(6) == "x")
                    {
                        amount = 0;
                        SendChatMessage("You didn't enter a number. Ex: !sell 2 for selling 2 keys.");
                    }
                    else
                    {
                        try
                        {
                            amount = int.Parse(message.Substring(6));
                        }
                        catch
                        {
                            SendChatMessage("You didn't put a valid number in. Example: !sell 2 for selling 2 keys");
                        }
                    }
                    // Check if the user has escrow.
                    if (Bot.GetEscrowDuration(new SteamID(OtherSID), "").DaysTheirEscrow > 2)
                    {
                        SendChatMessage("Sorry, You require to have an account with a valid Phone Authenticator.");
                    }
                    else
                    {
                        List<long> contextId = new List<long>();
                        contextId.Add(2);
                        contextId.Add(6);

                        mySteamInventory.load(440, contextId, Bot.SteamClient.SteamID);
                        OtherSteamInventory.load(440, contextId, OtherSID);
                        int KeysRequired = amount;
                        int MetalRequired = amount * buyPrice;
                        var offer = Bot.NewTradeOffer(OtherSID);
                        #region User-part
                        //Checking users stock.
                        foreach (GenericInventory.Item item in OtherSteamInventory.items.Values)
                        {
                            if (KeysRequired != 0)
                            {
                                if (OtherSteamInventory.getDescription(item.assetid).name == "Mann Co. Supply Crate Key" && OtherSteamInventory.getDescription(item.assetid).tradable)
                                {
                                    offer.Items.AddTheirItem(item.appid, item.contextid, (long)item.assetid);
                                    KeysRequired--;
                                }
                            }
                        }
                        if (KeysRequired != 0)
                        {
                            SendChatMessage("You dont have enough keys available for selling " + amount + " keys.");
                            notEnough = true;
                        }
                        #endregion
                        #region Bot-part
                        else
                        {
                            foreach (GenericInventory.Item item in mySteamInventory.items.Values)
                            {
                                if (mySteamInventory.getDescription(item.assetid).name == "Refined Metal" && mySteamInventory.getDescription(item.assetid).tradable && !UsedAssetIDs.Contains(item.assetid))
                                {
                                    if (MetalRequired >= 9)
                                    {

                                        offer.Items.AddMyItem(item.appid, item.contextid, (long)item.assetid);
                                        MetalRequired -= 9;
                                    }
                                }
                            }
                            foreach (GenericInventory.Item item in mySteamInventory.items.Values)
                            {
                                if (mySteamInventory.getDescription(item.assetid).name == "Reclaimed Metal" && mySteamInventory.getDescription(item.assetid).tradable && !UsedAssetIDs.Contains(item.assetid))
                                {
                                    if (MetalRequired >= 3 && MetalRequired <= 8)
                                    {

                                        offer.Items.AddMyItem(item.appid, item.contextid, (long)item.assetid);
                                        MetalRequired -= 3;
                                    }
                                }
                            }
                            foreach (GenericInventory.Item item in mySteamInventory.items.Values)
                            {
                                if (mySteamInventory.getDescription(item.assetid).name == "Scrap Metal" && mySteamInventory.getDescription(item.assetid).tradable && !UsedAssetIDs.Contains(item.assetid))
                                {
                                    if (MetalRequired >= 1 && MetalRequired < 9)
                                    {

                                        offer.Items.AddMyItem(item.appid, item.contextid, (long)item.assetid);
                                        MetalRequired -= 1;
                                    }
                                }
                            }
                            if (MetalRequired != 0)
                            {
                                Console.WriteLine(MetalRequired);
                                SendChatMessage("I dont have enough metal.");
                            }
                            #endregion
                            #region Sending Offer..
                            else
                            {
                                if (offer.Items.NewVersion)
                                {
                                    string newOfferId;
                                    offer.Send(out newOfferId, "Please leave a +rep if liked it!");
                                    if (!(newOfferId == null || newOfferId == String.Empty))
                                    {
                                        Bot.AcceptAllMobileTradeConfirmations();
                                        SendChatMessage("Thanks for trading with us. Your order (" + amount + " keys) has been sent.");
                                        SendChatMessage("You can accept it here: https://steamcommunity.com/tradeoffer/" + newOfferId + "/");
                                        Log.Success("Trade offer sent : Offer ID " + newOfferId);
                                        Bot.SteamFriends.SendChatMessage(ownerID, EChatEntryType.ChatMsg, "I've bought a key from " + Bot.SteamFriends.GetFriendPersonaName(OtherSID));
                                    }
                                }
                            }
                            #endregion
                        }
                    }
                }
            }
            #endregion
            #region Help
            else if (message.Contains("help"))
            {
                SendChatMessage("For first you have to check if I have keys or metals in my inventory. You can do it by writing \"stock\" (without quotation marks) to me. Also dont forget to check current price by writing \"price\". If you do this and you're sure I have things you want in my stock and you agree with price, use command \"!buy x\" or \"!sell x\" - \"x\" is amount of goods you want to buy. Right after doing that bot will send you trade offer with key(s) and metals.");
                SendChatMessage("*Example* - you want to buy 2 keys, so write !buy 2 and wait for offer.");
            }
            #endregion
            #region Stock
            else if (message.Contains("stock"))
            {

                int StockScrap = 0;
                int StockReclaimed = 0;
                int StockRefined = 0;
                int StockKey = 0;
                Bot.GetInventory();
                Inventory myInventory = Bot.MyInventory;
                foreach (Inventory.Item item in myInventory.Items)
                {
                    if (item.Defindex == 5000)
                    {
                        StockScrap++;
                    }
                    else if (item.Defindex == 5001)
                    {
                        StockReclaimed++;
                    }
                    else if (item.Defindex == 5002)
                    {
                        StockRefined++;
                    }
                    else if (item.Defindex == 5021)
                    {
                        StockKey++;
                    }
                }
                if ((StockScrap / 9.0) + (StockReclaimed / 3.0) + (StockRefined) < 10)
                {
                    Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "I have : " + string.Format("{0:0.000}", (StockScrap / 9.0) + (StockReclaimed / 3.0) + (StockRefined)).Substring(0, 4) + " refined. (" + StockRefined + " refined, " + StockReclaimed + " reclaimed, " + StockScrap + " scrap) and " + StockKey + " key(s).");
                }
                else
                {
                    Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "I have : " + string.Format("{0:0.000}", (StockScrap / 9.0) + (StockReclaimed / 3.0) + (StockRefined)).Substring(0, 5) + " refined. (" + StockRefined + " refined, " + StockReclaimed + " reclaimed, " + StockScrap + " scrap) and " + StockKey + " key(s).");
                }
            }
            #endregion
            #region Price
            else if (message.Contains("price") || message == ("p") || message.Contains("buying") || message.Contains("selling"))
            {
                Bot.SteamFriends.SendChatMessage(OtherSID, type, "I'm buying keys for " + string.Format("{0:0.000}", buyPrice / 9.0).Substring(0, 5) + " refined, and selling for " + string.Format("{0:0.000}", sellPrice / 9.0).Substring(0, 5) + " refined.");
            }
            #endregion
            #region Owner
            else if (message.Contains("owner") || message.Contains("mod") || message.Contains("admin"))
            {
                SendChatMessage("My owner is Krypton. ( https://steamcommunity.com/id/KryptonGas )");
            }
            #endregion
            #region Group
            else if (message.Contains("group") || message.Contains("community"))
            {
                SendChatMessage("You can get latest info, updates and helpful things in our group. ( http://steamcommunity.com/groups/TF2bot )");
                
            }
            #endregion
            #region Play
            else if (message.Contains(".play"))
            {
                if (IsAdmin)
                {

                    string PlayID = message.Substring(6);
                    if (PlayID.Contains("list"))
                    {
                        //List of GameIDs
                        Bot.SteamFriends.SendChatMessage(OtherSID, type, "List of GameIDs:");
                        Bot.SteamFriends.SendChatMessage(OtherSID, type, "440. Team Fortress 2");
                        Bot.SteamFriends.SendChatMessage(OtherSID, type, "570. Dota 2");
                        Bot.SteamFriends.SendChatMessage(OtherSID, type, "301520. RoboCraft");
                    }
                    if (PlayID.Contains("n"))
                    {
                        //No, None, Nothing etc.
                        Bot.SetGamePlaying(0);
                    }
                    else
                    {

                        // Play-Part
                        int PlayMessage = Int32.Parse(PlayID);
                        Bot.SetGamePlaying(PlayMessage);
                    }
                }

                else
                {
                    Bot.SteamFriends.SendChatMessage(OtherSID, type, "No.");
                }
            }
            #endregion
            #region Trade
            else if (message.Contains("trade"))
            {
                SendChatMessage("Sorry, You can't trade with normal trading, Use trade offers instead, Do command \"help\" to know how!");
            }
            #endregion
            #region Escrow
            else if (message.Contains("escrow") || (message.Contains("hold") && message.Contains("trade")))
            {
                bool HasEscrow = Bot.GetEscrowDuration(new SteamID(OtherSID), "").DaysTheirEscrow > 0;
                if (HasEscrow)
                {
                    SendChatMessage("You have escrow :( , Which means you are unable to trade with this bot.");
                    SendChatMessage("You can find more information about activating Phone Authentication here: https://support.steampowered.com/kb_article.php?ref=4440-RTUI-9218 .");
                }
                else
                {
                    SendChatMessage("You have no escrow!!! You are able to trade with this bot!");
                }
            }
            #endregion
            #region getauth
            else if (message == "getauth")
            {
                if (IsAdmin)
                {
                    try
                    {
                        SendChatMessage("Generated Steam Guard code: " + Bot.SteamGuardAccount.GenerateSteamGuardCode());
                    }
                    catch (NullReferenceException)
                    {
                        SendChatMessage("Unable to generate Steam Guard code.");
                    }
                }
            }
            #endregion
            #region Else
            else
            {
                SendChatMessage("Sorry, I dont know what you mean.. Available commands: \"!buy\", \"!sell\", \"help\", \"stock\", \"price\", \"owner\", \"group\".");
            }
            #endregion
        }

        public override bool OnGroupAdd() { return false; }

        public override bool OnFriendAdd()
        {
            if ((Bot.GetEscrowDuration(new SteamID(OtherSID), "").DaysTheirEscrow > 2))
            {
                SendChatMessage("Sorry, You got Escrow, This bot will not be useable for you. More info: http://steamcommunity.com/groups/TF2bot#announcements/detail/914600150799206334");
                Bot.SteamFriends.RemoveFriend(OtherSID);
                return false;
            }
            else
            {
                SendChatMessage("Do you want to buy keys to try your luck with opening crates or just for something else? Iam here for you. For first you have to check if I have keys or metals in my inventory. You can do it by writing 'stock' (without quotation marks) to me. If you do this and you're sure I have things you want in my stock, use command '!buy x' or '!sell x' - 'x' is amount of goods you want to buy. Right after doing that bot will send you trade offer with key/s and metals. *Example* - you want to buy 2 keys, so write !buy 2 and wait for offer.");
                
                Bot.SteamFriends.SendChatMessage(ownerID, EChatEntryType.ChatMsg, Bot.SteamFriends.GetFriendPersonaName(OtherSID) + "added me to his friends!");
                return true;
            }
        }

        public override void OnFriendRemove()
        {
            Bot.SteamFriends.SendChatMessage(ownerID, EChatEntryType.ChatMsg, Bot.SteamFriends.GetFriendPersonaName(OtherSID) + "removed me from his friends!");
        }

        public override void OnChatRoomMessage(SteamID chatID, SteamID sender, string message)
        {
            Log.Info(Bot.SteamFriends.GetFriendPersonaName(sender) + ": " + message);
            base.OnChatRoomMessage(chatID, sender, message);
        }

        public override void OnLoginCompleted()
        {
            GetOldFriends();
        }

        private void GetOldFriends()
        {
            int numFriends = Bot.SteamFriends.GetFriendCount();
            for (int count = 0; count < numFriends; count++)
            {
                SteamID friend = Bot.SteamFriends.GetFriendByIndex(count);
                ulong friendID = friend.ConvertToUInt64();
                
                if (Bot.SteamFriends.GetFriendPersonaState(friendID) == EPersonaState.Offline)
                {
                    
                    var jsonstring = new WebClient().DownloadString("http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=CC46B4B42227A9D657D47E406E372A88&steamids=" + friendID);

                    JsonValue value = JsonValue.Parse(jsonstring);
                    JsonObject result = value as JsonObject;
                    try
                    {
                        string lastlogoff = (string)result["response"]["players"][0]["lastlogoff"];

                        Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                        if ((unixTimestamp - 1210000) >= Int32.Parse(lastlogoff))
                        {
                            Bot.SteamFriends.RemoveFriend(friendID);
                            Console.WriteLine("Remove " + friendID);
                        }
                    }
                    catch
                    {
                        Bot.SteamFriends.RemoveFriend(friendID);
                        Console.WriteLine("Remove " + friendID + ". ERROR");
                    }
                }
                    
            }
            Console.WriteLine("Done loop.");
            
        }
        

        #region TradeStuff
        public override bool OnTradeRequest() { return false; }

        public override void OnTradeError(string error) { }

        public override void OnTradeTimeout() { }


        public override void OnTradeAwaitingConfirmation(long tradeOfferID)
        {
            SendChatMessage("You've got to answer a important confirmation.");
        }

        public override void OnTradeInit()
        {
            
        }

        public override void OnTradeAddItem(Schema.Item schemaItem, Inventory.Item inventoryItem) { }

        public override void OnTradeRemoveItem(Schema.Item schemaItem, Inventory.Item inventoryItem) { }

        public override void OnTradeMessage(string message) { }

        public override void OnTradeReady(bool ready) { }

        public override void OnTradeAccept() { }
        #endregion
    }
}
