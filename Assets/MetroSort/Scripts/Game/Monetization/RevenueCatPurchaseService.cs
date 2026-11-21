#if METROSORT_REVENUECAT
using System;
using System.Collections.Generic;
using MetroSort.Purchases;
using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>
    /// Google Play purchases through RevenueCat behind IPurchaseService. On start it reads the store prices in the
    /// player's own currency (they replace the fallback labels in StoreCatalog) and whether Remove Ads is owned.
    /// The coin packs are consumables; Remove Ads is a non-consumable unlocking the "no_ads" entitlement.
    /// </summary>
    public sealed class RevenueCatPurchaseService : IPurchaseService
    {
        private readonly string _apiKey, _entitlement;
        private global::Purchases _store;
        private bool _configured;

        /// <summary>Raised when the store reports Remove Ads as owned (a reinstall, another device).</summary>
        public event Action RemoveAdsOwned;

        public RevenueCatPurchaseService(string apiKey, string entitlement)
        {
            _apiKey = apiKey;
            _entitlement = string.IsNullOrEmpty(entitlement) ? "no_ads" : entitlement;
        }

        public void Initialize()
        {
            var go = new GameObject("RevenueCat");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _store = go.AddComponent<global::Purchases>();
            _store.useRuntimeSetup = true;
            // The component wires up its native side in Start: configure on the frame after.
            MonetizationRunner.After(0f, Configure);
        }

        private void Configure()
        {
            _store.Configure(global::Purchases.PurchasesConfiguration.Builder.Init(_apiKey).Build());
            _configured = true;
            var ids = new List<string>();
            foreach (StoreProduct p in StoreCatalog.Products) ids.Add(p.Id);
            _store.GetProducts(ids.ToArray(), (products, error) =>
            {
                if (error != null || products == null) return;
                foreach (global::Purchases.StoreProduct sp in products)
                {
                    StoreProduct mine = StoreCatalog.Find(sp.Identifier);
                    if (mine != null && !string.IsNullOrEmpty(sp.PriceString)) mine.PriceLabel = sp.PriceString;
                }
                StoreCatalog.NotifyPricesChanged();
            }, "inapp");
            _store.GetCustomerInfo((info, error) =>
            {
                if (info != null && Owns(info) && RemoveAdsOwned != null) RemoveAdsOwned();
            });
        }

        public void Purchase(string productId, Action<PurchaseResult> onFinished)
        {
            if (!_configured)
            {
                if (onFinished != null) onFinished(new PurchaseResult { ProductId = productId, Success = false, Error = "Store not ready" });
                return;
            }
            _store.PurchaseProduct(productId, result =>
            {
                var r = new PurchaseResult { ProductId = productId };
                if (result.UserCancelled || result.Error != null)
                {
                    r.Success = false;
                    r.Error = result.UserCancelled ? "Purchase cancelled" : result.Error.Message;
                }
                else
                {
                    r.Success = true;
                    r.Receipt = "revenuecat";
                    r.TransactionId = result.StoreTransaction != null ? result.StoreTransaction.TransactionIdentifier : null;
                    // StoreManager grants once per transaction id: never leave it empty.
                    if (string.IsNullOrEmpty(r.TransactionId)) r.TransactionId = "rc-" + productId + "-" + DateTime.UtcNow.Ticks;
                }
                if (onFinished != null) onFinished(r);
            }, "inapp");
        }

        public void RestorePurchases(Action<List<string>> onFinished)
        {
            if (!_configured)
            {
                if (onFinished != null) onFinished(new List<string>());
                return;
            }
            _store.RestorePurchases((info, error) =>
            {
                var owned = new List<string>();
                if (info != null && Owns(info)) owned.Add(StoreCatalog.RemoveAds);
                if (onFinished != null) onFinished(owned);
            });
        }

        private bool Owns(global::Purchases.CustomerInfo info)
        {
            if (info.Entitlements != null && info.Entitlements.Active != null && info.Entitlements.Active.ContainsKey(_entitlement)) return true;
            if (info.NonSubscriptionTransactions != null)
                foreach (global::Purchases.StoreTransaction t in info.NonSubscriptionTransactions)
                    if (t.ProductIdentifier == StoreCatalog.RemoveAds) return true;
            return false;
        }
    }
}
#endif
