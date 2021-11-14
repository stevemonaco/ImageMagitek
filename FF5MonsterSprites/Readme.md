Final Fantasy 5 SFC Monster Sprite Viewer

This tool reads the layout of FF5 Monster Sprites and displays them via ImageMagitek. The purpose is 
primarily to demonstrate how to use ImageMagitek in apps that are simpler than TileShop and to expand 
the abilities of ImageMagitek to new use cases. Requires "ff5.sfc" in application directory.

The information necessary largely comes from Squall_FF8's [article on FF5 Monster Graphics](https://www.ff6hacking.com/ff5wiki/index.php?title=Monster_Graphics)

There were two things to note:
1. The palette address calculation should multiply by 16, not 8.
2. The large size (16x16 tile) forms take 2 bytes per row of tiles. These must be endian-swapped. The small size forms are in byte-by-byte order.

[FF5 Sprite Viewer](Assets/screenshot.png)