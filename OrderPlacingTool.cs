// Decompiled with JetBrains decompiler
// Type: OrderPlacingTool.OrderPlacingTool
// Assembly: OrderPlacingTool, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: A639F7DA-FCA2-48A1-B5DB-C3BC906AC634
// Assembly location: C:\Users\LaDarrious\Desktop\Quantower\Settings\Scripts\Indicators\Order Placing Tool\OrderPlacingTool.dll

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;
using TradingPlatform.BusinessLayer.Native;
using TradingPlatform.BusinessLayer.Utils;

#nullable disable
namespace OrderPlacingTool
{
    public class OrderPlacingTool : Indicator
    {
        
        private const string VERSION = "1.04";
        private const string DESCRIPTION = "rectified button reset logic";
        private int riskInAmount = 100;
        private double rewardMultiplier = 1.0;
        private int xShift = 1000;
        private int yShift = 25;
        private PairColor limitButtonPairColor = new PairColor()
        {
            Text1 = "Back",
            Color1 = Color.Lime,
            Text2 = "Border",
            Color2 = Color.Lime
        };
        private PairColor marketButtonPairColor = new PairColor()
        {
            Text1 = "Back",
            Color1 = Color.Lime,
            Text2 = "Border",
            Color2 = Color.Lime
        };
        private Font font = new Font("Arial", 11f);
        private Color fontColor = Color.White;
        private PairColor infoButtonPairColor = new PairColor()
        {
            Text1 = "Back",
            Color1 = Color.Empty,
            Text2 = "Border",
            Color2 = Color.White
        };
        private Font infoFont = new Font("Arial", 11f);
        private Color infoFontColor = Color.White;
        private PairColor cancelButtonPairColor = new PairColor()
        {
            Text1 = "Back",
            Color1 = Color.Red,
            Text2 = "Border",
            Color2 = Color.Red
        };
        private Button limitButton = null;
        private Button marketButton = null;
        private Button infoButton = null;
        private bool initFailed = false;
        private DateTime expiry = new DateTime(2025, 6, 20, 0, 0, 0);
        private Brush limitBackBrush = null;
        private Pen limitBorderPen = null;
        private Brush marketBackBrush = null;
        private Pen marketBorderPen = null;
        private Brush textBrush = null;
        private StringFormat stringFormat = new StringFormat()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        private Brush infoBackBrush = null;
        private Pen infoBorderPen = null;
        private Brush infoTextBrush = null;
        private Brush cancelBackBrush = null;
        private Pen cancelBorderPen = null;
        private TradeParams tradeParams = new TradeParams();
        private bool isMouseRegistered = false;
        private TradingOperationResult tradingOperationResult = null;

        // 1. Override the Settings property instead of redeclaring it as virtual
        public override IList<SettingItem> Settings
        {
            get
            {
                IList<SettingItem> settings = base.Settings;
                SettingItemSeparatorGroup sep = settings.FirstOrDefault()?.SeparatorGroup;
                settings.Add(new SettingItemInteger("riskInAmount", riskInAmount)
                {
                    Text = "Risk in amount",
                    SeparatorGroup = sep
                });
                settings.Add(new SettingItemDouble("rewardMultiplier", rewardMultiplier)
                {
                    Text = "Reward multiplier for TP",
                    Increment = 0.1,
                    DecimalPlaces = 1,
                    SeparatorGroup = sep
                });
                settings.Add(new SettingItemInteger("xShift", xShift)
                {
                    Text = "X shift",
                    SeparatorGroup = sep
                });
                settings.Add(new SettingItemInteger("yShift", yShift)
                {
                    Text = "Y shift",
                    SeparatorGroup = sep
                });
                settings.Add(new SettingItemPairColor("limitButtonPairColor", limitButtonPairColor)
                {
                    Text = "Limit button colors",
                    SeparatorGroup = sep
                });
                settings.Add(new SettingItemPairColor("marketButtonPairColor", marketButtonPairColor)
                {
                    Text = "Market button colors",
                    SeparatorGroup = sep
                });
                settings.Add(new SettingItemFont("font", font)
                {
                    Text = "Font style",
                    SeparatorGroup = sep
                });
                settings.Add(new SettingItemColor("fontColor", fontColor)
                {
                    Text = "Font color",
                    SeparatorGroup = sep
                });
                settings.Add(new SettingItemPairColor("infoButtonPairColor", infoButtonPairColor)
                {
                    Text = "Info box colors",
                    SeparatorGroup = sep
                });
                settings.Add(new SettingItemFont("infoFont", infoFont)
                {
                    Text = "Info font style",
                    SeparatorGroup = sep
                });
                settings.Add(new SettingItemColor("infoFontColor", infoFontColor)
                {
                    Text = "Info font color",
                    SeparatorGroup = sep
                });
                settings.Add(new SettingItemPairColor("cancelButtonPairColor", cancelButtonPairColor)
                {
                    Text = "Cancel button colors",
                    SeparatorGroup = sep
                });
                return settings;
            }
            set
            {
                SettingItemExtensions.TryGetValue(value, "riskInAmount", out riskInAmount);
                SettingItemExtensions.TryGetValue(value, "rewardMultiplier", out rewardMultiplier);
                SettingItemExtensions.TryGetValue(value, "xShift", out xShift);
                SettingItemExtensions.TryGetValue(value, "yShift", out yShift);
                SettingItemExtensions.TryGetValue(value, "limitButtonPairColor", out limitButtonPairColor);
                SettingItemExtensions.TryGetValue(value, "marketButtonPairColor", out marketButtonPairColor);
                SettingItemExtensions.TryGetValue(value, "font", out font);
                SettingItemExtensions.TryGetValue(value, "fontColor", out fontColor);
                SettingItemExtensions.TryGetValue(value, "infoButtonPairColor", out infoButtonPairColor);
                SettingItemExtensions.TryGetValue(value, "infoFont", out infoFont);
                SettingItemExtensions.TryGetValue(value, "infoFontColor", out infoFontColor);
                SettingItemExtensions.TryGetValue(value, "cancelButtonPairColor", out cancelButtonPairColor);
                base.Settings = value;
                OnSettingsUpdated();
            }
        }

        private void OnSettingsUpdated()
        {
            // reposition buttons
            int w = 100, h = 40, s = 10;
            limitButton?.Set(xShift, yShift, xShift + w, yShift + h);
            marketButton?.Set(xShift + w + s, yShift, xShift + 2 * w + s, yShift + h);
            infoButton?.Set(xShift + (w + s) / 2, yShift + h + s, xShift + (w + s) / 2 + 2 * w, yShift + 2 * h + s);
            // 2. Use string interpolation instead of DefaultInterpolatedStringHandler
            if (infoButton != null)
                infoButton.text = $"$Risk = {riskInAmount}, TP reward = {rewardMultiplier}";
            // update brushes
            limitBackBrush = new SolidBrush(limitButtonPairColor.Color1);
            limitBorderPen = new Pen(limitButtonPairColor.Color2);
            marketBackBrush = new SolidBrush(marketButtonPairColor.Color1);
            marketBorderPen = new Pen(marketButtonPairColor.Color2);
            textBrush = new SolidBrush(fontColor);
            infoBackBrush = new SolidBrush(infoButtonPairColor.Color1);
            infoBorderPen = new Pen(infoButtonPairColor.Color2);
            infoTextBrush = new SolidBrush(infoFontColor);
            cancelBackBrush = new SolidBrush(cancelButtonPairColor.Color1);
            cancelBorderPen = new Pen(cancelButtonPairColor.Color2);
            // 2b. Set ShortName via interpolation
            ShortName = $"{Name}({riskInAmount},{rewardMultiplier})";
        }

        // 5. Mark override
        public override void Dispose()
        {
            Core.Instance.Loggers.Log("Dispose()", LoggingLevel.System, null);
            if (isMouseRegistered)
            {
                isMouseRegistered = false;
                CurrentChart.MouseClick -= CurrentChart_MouseClick;
                CurrentChart.AccountChanged -= CurrentChart_AccountChanged;
                ExecutionEntity.Core.OrderAdded -= Core_OrderAdded;
                ExecutionEntity.Core.OrdersHistoryAdded -= Core_OrdersHistoryAdded;
            }
            limitButton = null;
            marketButton = null;
            infoButton = null;
            base.Dispose();
        }

        public OrderPlacingTool()
        {
            Name = "MBS Order Placing Tool";
            Description = DESCRIPTION;
            AddLineSeries("line1", Color.CadetBlue, 1, LineStyle.Solid);
            SeparateWindow = false;
        }

        // 5. Mark override
        protected override void OnInit()
        {
            initFailed = false;
            int w = 100, h = 40, s = 10;
            if (limitButton == null)
                limitButton = new Button("Limit", xShift, yShift, xShift + w, yShift + h, false);
            if (marketButton == null)
                marketButton = new Button("Market", xShift + w + s, yShift, xShift + 2 * w + s, yShift + h, false);
            if (infoButton == null)
                infoButton = new Button($"$Risk = {riskInAmount}, TP reward = {rewardMultiplier}",
                    xShift + (w + s) / 2, yShift + h + s, xShift + (w + s) / 2 + 2 * w, yShift + 2 * h + s, false);
            if (!isMouseRegistered)
            {
                isMouseRegistered = true;
                CurrentChart.MouseClick += CurrentChart_MouseClick;
                CurrentChart.AccountChanged += CurrentChart_AccountChanged;
                ExecutionEntity.Core.OrderAdded += Core_OrderAdded;
                ExecutionEntity.Core.OrdersHistoryAdded += Core_OrdersHistoryAdded;
            }
            tradeParams.symbol = HistoricalData.Symbol;
            tradeParams.account = CurrentChart.Account;
            limitBackBrush ??= new SolidBrush(limitButtonPairColor.Color1);
            limitBorderPen ??= new Pen(limitButtonPairColor.Color2);
            marketBackBrush ??= new SolidBrush(marketButtonPairColor.Color1);
            marketBorderPen ??= new Pen(marketButtonPairColor.Color2);
            textBrush ??= new SolidBrush(fontColor);
            infoBackBrush ??= new SolidBrush(infoButtonPairColor.Color1);
            infoBorderPen ??= new Pen(infoButtonPairColor.Color2);
            infoTextBrush ??= new SolidBrush(infoFontColor);
            cancelBackBrush ??= new SolidBrush(cancelButtonPairColor.Color1);
            cancelBorderPen ??= new Pen(cancelButtonPairColor.Color2);
        }

        // 5. Mark override
        protected override void OnUpdate(UpdateArgs args)
        {
            // no update logic
        }

        // 5. Mark override
        public override void OnPaintChart(PaintChartEventArgs args)
        {
            if (initFailed || HistoricalData.Count != Count)
                return;
            base.OnPaintChart(args);
            var graphics = args.Graphics;
            var clipBounds = graphics.ClipBounds;
            // 4. Add CombineMode
            graphics.SetClip(args.Rectangle, CombineMode.Replace);
            try
            {
                if (limitButton != null)
                {
                    graphics.FillRectangle(limitButton.text == "Cancel" ? cancelBackBrush : limitBackBrush,
                        limitButton.x1, limitButton.y1, limitButton.x2 - limitButton.x1, limitButton.y2 - limitButton.y1);
                    graphics.DrawRectangle(limitButton.text == "Cancel" ? cancelBorderPen : limitBorderPen,
                        limitButton.x1, limitButton.y1, limitButton.x2 - limitButton.x1, limitButton.y2 - limitButton.y1);
                    graphics.DrawString(limitButton.text, font, textBrush,
                        (float)((limitButton.x1 + limitButton.x2) / 2),
                        (float)((limitButton.y1 + limitButton.y2) / 2), stringFormat);
                }
                if (marketButton != null)
                {
                    graphics.FillRectangle(marketButton.text == "Cancel" ? cancelBackBrush : marketBackBrush,
                        marketButton.x1, marketButton.y1, marketButton.x2 - marketButton.x1, marketButton.y2 - marketButton.y1);
                    graphics.DrawRectangle(marketButton.text == "Cancel" ? cancelBorderPen : marketBorderPen,
                        marketButton.x1, marketButton.y1, marketButton.x2 - marketButton.x1, marketButton.y2 - marketButton.y1);
                    graphics.DrawString(marketButton.text, font, textBrush,
                        (float)((marketButton.x1 + marketButton.x2) / 2),
                        (float)((marketButton.y1 + marketButton.y2) / 2), stringFormat);
                }
                if (infoButton == null)
                    return;
                graphics.FillRectangle(infoBackBrush,
                    infoButton.x1, infoButton.y1, infoButton.x2 - infoButton.x1, infoButton.y2 - infoButton.y1);
                graphics.DrawRectangle(infoBorderPen,
                    infoButton.x1, infoButton.y1, infoButton.x2 - infoButton.x1, infoButton.y2 - infoButton.y1);
                graphics.DrawString(infoButton.text, infoFont, infoTextBrush,
                    (float)((infoButton.x1 + infoButton.x2) / 2),
                    (float)((infoButton.y1 + infoButton.y2) / 2), stringFormat);
            }
            finally
            {
                // restore region
                graphics.SetClip(clipBounds, CombineMode.Replace);
            }
        }
        private void CurrentChart_AccountChanged(object sender, ChartEventArgs e)
        {
            // keep your tradeParams.account in sync
            this.tradeParams.account = this.CurrentChart.Account;
        }

        private void Core_OrderAdded(Order obj) { }
        private void Core_OrdersHistoryAdded(OrderHistory obj) { }

        private void CurrentChart_MouseClick(object sender, ChartMouseNativeEventArgs mouse)
        {
            if (initFailed || mouse == null || CurrentChart == null || CurrentChart.Account == null ||
                !CurrentChart.MainWindow.ClientRectangle.Contains(((NativeMouseEventArgs)mouse).Location) ||
                ((NativeMouseEventArgs)mouse).Button != NativeMouseButtons.Left ||
                tradeParams.symbol == null || tradeParams.account == null ||
                DoubleExtensions.IsNanOrDefault(tradeParams.symbol.TickSize))
                return;
            int x = ((NativeMouseEventArgs)mouse).X;
            int y = ((NativeMouseEventArgs)mouse).Y;
            if (limitButton.Contains(x, y))
            {
                if (limitButton.isClicked)
                {
                    limitButton.isClicked = false;
                    limitButton.text = "Limit";
                    tradeParams.Reset();
                }
                else
                {
                    limitButton.isClicked = true;
                    limitButton.text = "Cancel";
                    marketButton.Reset("Market");
                }
            }
            else if (marketButton.Contains(x, y))
            {
                if (marketButton.isClicked)
                {
                    marketButton.isClicked = false;
                    marketButton.text = "Market";
                    tradeParams.Reset();
                }
                else
                {
                    marketButton.isClicked = true;
                    marketButton.text = "Cancel";
                    limitButton.Reset("Limit");
                }
            }
            else
            {
                // price selection logic
                if (limitButton.isClicked)
                {
                    var mainWindow = CurrentChart.MainWindow;
                    if (tradeParams.price == 0) tradeParams.price = mainWindow.CoordinatesConverter.GetPrice(y);
                    else if (tradeParams.slPrice == 0) tradeParams.slPrice = mainWindow.CoordinatesConverter.GetPrice(y);
                    if (tradeParams.price != 0 && tradeParams.slPrice != 0)
                    {
                        tradeParams.orderTypeId = "Limit";
                        // 3. Check against Side.Buy rather than null
                        tradeParams.side = tradeParams.price > tradeParams.slPrice ? Side.Buy : Side.Sell;
                        tradeParams.price = tradeParams.symbol.RoundPriceToTickSize(tradeParams.price, double.NaN);
                        tradeParams.slPrice = tradeParams.symbol.RoundPriceToTickSize(tradeParams.slPrice, double.NaN);
                        double slTicks = tradeParams.side == Side.Buy
                            ? (tradeParams.price - tradeParams.slPrice) / tradeParams.symbol.TickSize
                            : (tradeParams.slPrice - tradeParams.price) / tradeParams.symbol.TickSize;
                        double tpTicks = slTicks * rewardMultiplier;
                        tradeParams.lotSize = GetVolumeByFixedAmount(tradeParams.symbol, riskInAmount, slTicks);
                        Task.Run(() =>
                        {
                            var req = new PlaceOrderRequestParameters();
                            req.Symbol = tradeParams.symbol;
                            req.Account = tradeParams.account;
                            req.OrderTypeId = tradeParams.orderTypeId;
                            req.Side = tradeParams.side;
                            req.Price = tradeParams.price;
                            req.Quantity = tradeParams.lotSize;
                            req.StopLoss = SlTpHolder.CreateSL(slTicks, PriceMeasurement.Offset, false, double.NaN, double.NaN);
                            req.TakeProfit = rewardMultiplier > 0
                                ? SlTpHolder.CreateTP(tpTicks, PriceMeasurement.Offset, double.NaN, double.NaN)
                                : null;
                            tradingOperationResult = Core.PlaceOrder(req);
                            tradeParams.Reset();
                        });
                    }
                }
                else if (marketButton.isClicked)
                {
                    var mainWindow = CurrentChart.MainWindow;
                    tradeParams.price = tradeParams.symbol.Bid;
                    if (tradeParams.slPrice == 0) tradeParams.slPrice = mainWindow.CoordinatesConverter.GetPrice(y);
                    if (tradeParams.price != 0 && tradeParams.slPrice != 0)
                    {
                        tradeParams.orderTypeId = "Market";
                        tradeParams.side = tradeParams.price > tradeParams.slPrice ? Side.Buy : Side.Sell;
                        tradeParams.price = tradeParams.symbol.RoundPriceToTickSize(tradeParams.price, double.NaN);
                        tradeParams.slPrice = tradeParams.symbol.RoundPriceToTickSize(tradeParams.slPrice, double.NaN);
                        double slTicks = tradeParams.side == Side.Buy
                            ? (tradeParams.price - tradeParams.slPrice) / tradeParams.symbol.TickSize
                            : (tradeParams.slPrice - tradeParams.price) / tradeParams.symbol.TickSize;
                        double tpTicks = slTicks * rewardMultiplier;
                        tradeParams.lotSize = GetVolumeByFixedAmount(tradeParams.symbol, riskInAmount, slTicks);
                        Task.Run(() =>
                        {
                            var req = new PlaceOrderRequestParameters();
                            req.Symbol = tradeParams.symbol;
                            req.Account = tradeParams.account;
                            req.OrderTypeId = tradeParams.orderTypeId;
                            req.Side = tradeParams.side;
                            req.Quantity = tradeParams.lotSize;
                            req.StopLoss = SlTpHolder.CreateSL(slTicks, PriceMeasurement.Offset, false, double.NaN, double.NaN);
                            req.TakeProfit = rewardMultiplier > 0
                                ? SlTpHolder.CreateTP(tpTicks, PriceMeasurement.Offset, double.NaN, double.NaN)
                                : null;
                            tradingOperationResult = Core.PlaceOrder(req);
                            tradeParams.Reset();
                        });
                    }
                }
            }
        }

        private double GetVolumeByFixedAmount(Symbol symbol, int amountToRisk, double slTicks)
        {
            if (DoubleExtensions.IsNanOrDefault(symbol.Bid) || DoubleExtensions.IsNanOrDefault(symbol.TickSize))
                return 0;
            double num1 = symbol.GetTickCost(symbol.Bid) * slTicks;
            double num2 = (double)amountToRisk / num1;
            var loggers = Core.Instance.Loggers;
            loggers.Log($"symbol={symbol}, amountToRisk={amountToRisk}, slTicks={slTicks}, tickSize={symbol.TickSize}, tickCost={symbol.GetTickCost(symbol.Bid)}, calculatedLotSize={num2}", LoggingLevel.System, null);
            double minLot = symbol.MinLot;
            double maxLot = symbol.MaxLot;
            double lotStep = symbol.LotStep;
            double adjusted = Math.Max(0, num2 - minLot);
            int steps = (int)(adjusted / lotStep);
            double volumeByFixedAmount = minLot + steps * lotStep;
            if (volumeByFixedAmount < minLot) volumeByFixedAmount = minLot;
            if (volumeByFixedAmount > maxLot) volumeByFixedAmount = maxLot;
            return volumeByFixedAmount;
        }

        private class Button
        {
            public string text;
            public int x1;
            public int y1;
            public int x2;
            public int y2;
            public bool isClicked;

            public Button(string text, int x1, int y1, int x2, int y2, bool isClicked)
            {
                this.text = text;
                this.x1 = x1;
                this.y1 = y1;
                this.x2 = x2;
                this.y2 = y2;
                this.isClicked = isClicked;
            }

            public void Set(int x1, int y1, int x2, int y2)
            {
                this.x1 = x1;
                this.y1 = y1;
                this.x2 = x2;
                this.y2 = y2;
            }

            public bool Contains(int x, int y)
            {
                return x >= x1 && x < x2 && y >= y1 && y < y2;
            }

            public void Reset(string text)
            {
                this.text = text;
                this.isClicked = false;
            }
        }

        private class TradeParams
        {
            public Symbol symbol;
            public Account account;
            public string orderTypeId;
            public Side side;
            public double lotSize;
            public double price;
            public double slPrice;
            public double tpPrice;

            public void Reset()
            {
                this.orderTypeId = null;
                this.side = Side.Buy;
                this.lotSize = 0;
                this.price = 0;
                this.slPrice = 0;
                this.tpPrice = 0;
            }
        }
    }
}
