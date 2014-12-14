AdKats Extension - LRT - Loadout Enforcer
==========

This plugin currently enforces infantry loadouts to whatever specifications you desire, with about 4000 options. This plugin is an extension for AdKats, and thus requires the latest test version (5.3.0.4 or later) to be installed and running.

Documentation on LRT has yet to be fully completed, as the plugin is still relatively private, however, it does fully function. 
Some things you will need to know, as documentation is not done yet:

 * This plugin is still under development, and is being updated daily/weekly, expect changes.
 * There are no debug level options at the moment, what you see in the console is what you get. Options will be added soon.
 * This extension changes the way the /mark command in AdKats works, if you use that command on a player it marks them for loadout enforcement instead of leave notification.
 * There are 2 levels of enforcement:
 * * Trigger enforcement
 * * * (Applies to problem players only)
 * * * Players under trigger enforcement are those with marks/punishments against them during the current session, or additionally if admins are offline they fall under this enforcement because of high infraction points or by being reported by another player. Trigger enforcement happens on spawn, and additionaly on any mark/punish/report issued against them.
 * * Spawn enforcement
 * * * (Applies to all* players)
 * * * When you deny a certain weapon for trigger enforcement, a second setting appears below it asking if it should also be denied on spawn. Items under spawn enforcement are acted on for *all non-admin non-reputable players. Spawn enforcement happens on spawn.
 * Battlelog can take 2-4 seconds to update a player's loadout after they change it, so loadouts are checked 5 seconds after a player spawns.
 * Once you add a weapon/attachement as denied, new settings appear at the bottom of the list, these are custom messages sent to players when they spawn under restriction with those weapons. Modify them as needed.
 * None of the vehicle attachements work yet, as I have not placed this under testing on a vehicle centric server yet.

Install:
Once paid, the server it's for linked, and your github username provided, you will be given view-only access to the following repository:
https://github.com/AdKats/AdKats-LRT
Access to this repository allows you to get the tokens needed to install the plugin.

The first token is the plugin itself: 
https://github.com/AdKats/AdKats-LRT...t/AdKatsLRT.cs
Click 'Raw', and copy the token you see in the URL. Paste that token in AdKats setting 'A14. External Command Settings|AdkatsLRT Extension Token'. Once that token is added, Adkats will automatically download and update the plugin when changes are made.

Once that is done, reboot procon. You will now see the AdKats LRT plugin, with 2 settings, tokens. The first token is required, the second unused and will be removed soon.
The first setting in the plugin is the WARSAW Token. Use the same method as before to get the token from here:
https://github.com/AdKats/AdKats-LRT...wCodeBook.json
Paste the token into the WARSAW Token setting.

Start the plugin. It will confirm AdKats is installed/running, download the WARSAW codebook and decode it, and wait for first player list from AdKats. When the first player list comes through, loadout enforcement will be online.

Your access to the repository will be permanent, so if the tokens expire, simply use the same process to grab updated ones.
