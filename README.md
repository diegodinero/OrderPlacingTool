<img width="2761" height="2227" alt="image" src="https://github.com/user-attachments/assets/41a26c6f-a9b1-4eec-99e7-c427fe49a692" />


Quantower Chart‐based tool that allows a trader to visually place, adjust, and manage orders directly from the chart with minimal typing or menu navigation. It’s designed to be fast, mouse‐driven, and to display critical trade parameters such as break‐even (BE) ticks in real‐time.

Core Functionalities
Chart‐Based Order Placement,

The tool uses custom‐drawn buttons on the chart for placing:

Market orders (Buy / Sell immediately)

Limit orders (Buy/Sell at a specified better price)

Stop orders (Buy/Sell at a specified worse price to enter)

Flatten all (Close all positions instantly)

Buttons are drawn at fixed screen coordinates and respond to mouse clicks.

Mouse Click Trading Logic,

Left Click → Places the order type tied to the clicked button.

Limit Order Button: Places a buy/sell limit order at the clicked price level on the chart. Second click is stop loss

Stop Order Button: Places a buy/sell stop order at the clicked price level.  Second click is stop loss

Market Order Buttons: Sends immediate buy/sell orders at current market price. Click for stop loss

Flatten Button: Closes all open positions and clears internal trade state.

Profit/Loss/Break‐Even (BE) Tracking,

The indicator monitors the last entry price when an order is placed.

It calculates the number of ticks between the current price and the entry price.

Displays this as a floating text label on the chart, updating in real‐time.

Useful for quickly seeing how far away you are from breaking even without checking account details.

Adjustable Stop Loss (SL) and Take Profit (TP),

Supports quick placement of SL and TP orders relative to an entry.

The “Move SL to BE” function lets you shift stop loss to the entry price with one click.
