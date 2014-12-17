<script>
    //<latest_stable_release>1.0.4.1</latest_stable_release>
</script>
<h1>AdKats Extension - LRT - Loadout Enforcer</h1>
<h2>Overview</h2>
<p>
    AdKatsLRT allows for enforcement of any infantry loadout to the server owner's specs. It has automatic integration with AdKats systems, including punishments, infractions, reports, and marking.
</p>
<ul>
    <li>
        It can enforce every infantry item (any primary, secondary, attachments for either, gadgets, knifes, and grenades) in the game.
    </li>
    <li>
        Any update made to the game's weapons are automatically imported and made available, so if DICE changes or adds weapons, they are immediately enforceable.
    </li>
    <li>
        Customizable kill messages for each denied item, with combined messages and details if more than one is spawned in the same loadout.
    </li>
    <li>
        Players notified and thanked when they fix their loadouts after being killed.
    </li>
    <li>
        Two levels of enforcement, allowing multiple levels of severity for each item.
    </li>
    <li>
        Using the reputation system, reputable players are not forced to change their loadouts, as we know they are not going to use them.
    </li>
    <li>
        Admins are whitelisted from spawn enforcment, but still fall under trigger enforcement if they are marked/punished.
    </li>
    <li>
        Through AdKats, other plugins can call loadout checks and enforcement, so it can enhance your current autoadmin.
    </li>
    <li>
        Statistics on enforcement, including percent of players enforced, percent killed for enforcement, percent who fixed their loadouts after kill, and percent who quit the server without fixing their loadouts after kill.
    </li>
</ul>
<p>
    <a href="https://forum.myrcon.com/showthread.php?9180" name=thread>
        <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Thread.jpg" alt="AdKatsLRT Thread">
    </a>
</p>
<p>
    Development by Daniel J. Gradinjan (ColColonCleaner)
</p>
<p>
    If you find any bugs, please inform me about them on the MyRCON forums and they will be fixed ASAP.
</p>
<br/>
<HR>
<br/>
<p>
    <a name=manual />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_UserManual.jpg" alt="AdKats User Manual">
</p>
<p>
    <a name=dependencies />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Dependencies.jpg" alt="AdKats User Manual">
</p>
<h4>1. AdKats</h4>
<p>
    AdKats 5.3.1.3 or later, and its dependencies.
</p>
<HR>
<p>
    <a name=install />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Install.jpg" alt="AdKats User Manual">
</p>
<ol>
    <li>
        <b>Install AdKats.</b>
        Download and install the latest version of AdKats.
        <a href="https://github.com/AdKats/AdKats#install" target="_blank">AdKats Install Instructions.</a>
        After install make sure you are running version 5.3.1.3 or later. If you are not, download the latest test version from here:
        <a href="https://github.com/AdKats/AdKats#install" target="_blank">AdKats Test Branch.</a>
    </li>
    <li>
        <b>Purchase AdKatsLRT, and aquire an extension token.</b>
        Private message ColColonCleaner on the MyRCON forums about getting the plugin. Once approved and purchased, you will be given an extension token, usable to install AdKatsLRT.
    </li>
    <li>
        <b>Install the plugin.</b>
        Once purchased, and your extension token aquired, simply paste the token in AdKats setting "A14. External Command Settings | AdKatsLRT Extension Token". The plugin will be automatically downloaded and installed onto your procon instance. Restart procon to see the plugin.
    </li>
    <li>
        <b>Enable AdKatsLRT.</b>
        AdKatsLRT will download the WARSAW library, parse its contents into usable settings, and those settings will appear. It will then wait for the first player list response from AdKats. Once that comes through, it will fully enable and loadout enforcement will be online. Enjoy your new admin tool!
    </li>
</ol>
<p>
    If you have any problems installing AdKatsLRT please let me know on the MyRCON forums and I'll respond promptly.
</p>
<HR>
<p>
    <a name=features />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Features.jpg" alt="AdKats User Manual">
</p>
<h3>Item Library</h3>
<p>
    Every infantry item in the game (about 3500 items), can be enforced here. The settings are split into 3 sections; Weapons, Gadgets, and Weapon Accessories, in that order.
</p>
<h3>Item Library</h3>
<p>
    Once a user is added you need to assign their soldiers.
    If you add a user with the same name as their soldier(s), their soldier(s) will be added automatically.
    Users can have multiple soldiers, so if your admins have multiple accounts you can assign all of those soldiers
    under their user.
    All soldiers added need to be in your database before they can be added to a user.
    This system tracks user's soldiers, so if they change their soldier names they will still have powers without
    needing to contact admins about the name change.
    Type their soldier's name in the "new soldier" field to add them.
    It will error out if it cannot find the soldier in the database.
    To add soldiers to the database quickly after installing stat logger for the first time, have them join any server
    you are running this version of AdKats on and their information will be immediately added.
</p>
<p>
    The user list is sorted by role ID, then by user name.
    Any item that says "Delete?" you need to type the word delete in the line and press enter.
</p>
<h3>Full Logging</h3>
<p>
    All commands, their usage, who used them, who they were targeted on, why, when they were used, and where from, are
    logged in the database.
    All plugin actions are additionally stored in Procon's event log for review without connecting to the database.
    Player's name/IP changes are logged and the records connected to their player ID, so tracking players is easier.
</p>
<HR>
<p>
    <a name=commands />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Commands.jpg" alt="AdKats User Manual">
</p>
<p>
    Certain commands in AdKats are modified by this plugin. The changes to those commands are listed below.
</p>
<table>
<tr>
    <td><b>Command</b></td>
    <td><b>Default Text</b></td>
    <td><b>Changes</b></td>
</tr>
<tr>
    <td><b>Punish Player</b></td>
    <td>punish</td>
    <td>
        Punish works as normal, but also initiates trigger level enforcement on the target player for the duration the plugin is online.
    </td>
</tr>
<tr>
    <td><b>Mark Player</b></td>
    <td>mark</td>
    <td>
        Instead of marking a player for leave notification only, it also initiates trigger level enforcement on the target player for the duration the plugin is online.
    </td>
</tr>
<tr>
    <td><b>Report Player</b></td>
    <td>report</td>
    <td>
        Reports initiate trigger level enforcement on targeted players. If the reported player has invalid items in their loadout, the report is automatically accepted, and admins notified of such.
    </td>
</tr>
<tr>
    <td><b>Call Admin</b></td>
    <td>admin</td>
    <td>
        Same changes as Report Player.
    </td>
</tr>
</table>
<HR>
<p>
    <a name=settings />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Settings.jpg" alt="AdKats User Manual">
</p>
<h3>0. Instance Settings:</h3>
<ul>
    <li><b>'Auto-Enable/Keep-Alive'</b> - When this is enabled, AdKats will auto-recover from shutdowns and auto-restart
        if disabled.
    </li>
</ul>
<h3>1. Server Settings:</h3>
<ul>
    <li><b>'Lock Settings - Create Password'</b> - Lock settings with a new created password > 5 characters.</li>
    <li><b>'Lock Settings'</b> - Lock settings with the existing settings password.</li>
    <li><b>'Unlock Settings'</b> - Unlock settings with the existing settings password.</li>
    <li><b>'Setting Import'</b> - Enter an existing server ID here and all settings from that instance will be imported
        here. All settings on this instance will be overwritten.<br/></li>
    <li><b>'Server ID (Display)'</b> - ID of this server. Automatically set via the database.</li>
    <li><b>'Server IP (Display)'</b> - IP address and port of this server. Automatically set via Procon.<br/></li>
    <li><b>'Low Population Value'</b> - Number of players at which the server is deemed 'Low Population'.</li>
</ul>
<h3>A10. Admin Assistant Settings:</h3>
<ul>
    <li><b>'Enable Admin Assistants'</b> - Whether admin assistant statuses can be assigned to players.</li>
    <li><b>'Minimum Confirmed Reports Per Month'</b> - How many confirmed reports the player must have in the past month
        to be considered an admin assistant.
    </li>
    <li><b>'Enable Admin Assistant Perk'</b> - Whether admin assistants will get the TeamSwap perk for their help.</li>
    <li><b>'Use AA Report Auto Handler'</b> - Whether the internal auto-handling system for admin assistant reports is
        enabled.
    </li>
    <li><b>'Auto-Report-Handler Strings'</b> - List of trigger words/phrases that the auto-handler will act on. One per
        line.
    </li>
</ul>
<h3>D99. Debug Settings:</h3>
<ul>
    <li><b>'Debug level'</b> -
        Indicates how much debug-output is printed to the plugin-console.
        0 turns off debug messages (just shows important warnings/exceptions/success), 5 is most detailed.
    </li>
</ul>
