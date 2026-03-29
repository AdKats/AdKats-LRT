using System;
using System.Collections.Generic;
using System.Linq;

namespace PRoConEvents
{
    public partial class AdKatsLRT
    {
        public void PopulateMapModes()
        {
            _availableMapModes = new List<MapMode> {
                new MapMode(1, "ConquestLarge0", "MP_Abandoned", "Conquest Large", "Zavod 311"),
                new MapMode(2, "ConquestLarge0", "MP_Damage", "Conquest Large", "Lancang Dam"),
                new MapMode(3, "ConquestLarge0", "MP_Flooded", "Conquest Large", "Flood Zone"),
                new MapMode(4, "ConquestLarge0", "MP_Journey", "Conquest Large", "Golmud Railway"),
                new MapMode(5, "ConquestLarge0", "MP_Naval", "Conquest Large", "Paracel Storm"),
                new MapMode(6, "ConquestLarge0", "MP_Prison", "Conquest Large", "Operation Locker"),
                new MapMode(7, "ConquestLarge0", "MP_Resort", "Conquest Large", "Hainan Resort"),
                new MapMode(8, "ConquestLarge0", "MP_Siege", "Conquest Large", "Siege of Shanghai"),
                new MapMode(9, "ConquestLarge0", "MP_TheDish", "Conquest Large", "Rogue Transmission"),
                new MapMode(10, "ConquestLarge0", "MP_Tremors", "Conquest Large", "Dawnbreaker"),
                new MapMode(11, "ConquestSmall0", "MP_Abandoned", "Conquest Small", "Zavod 311"),
                new MapMode(12, "ConquestSmall0", "MP_Damage", "Conquest Small", "Lancang Dam"),
                new MapMode(13, "ConquestSmall0", "MP_Flooded", "Conquest Small", "Flood Zone"),
                new MapMode(14, "ConquestSmall0", "MP_Journey", "Conquest Small", "Golmud Railway"),
                new MapMode(15, "ConquestSmall0", "MP_Naval", "Conquest Small", "Paracel Storm"),
                new MapMode(16, "ConquestSmall0", "MP_Prison", "Conquest Small", "Operation Locker"),
                new MapMode(17, "ConquestSmall0", "MP_Resort", "Conquest Small", "Hainan Resort"),
                new MapMode(18, "ConquestSmall0", "MP_Siege", "Conquest Small", "Siege of Shanghai"),
                new MapMode(19, "ConquestSmall0", "MP_TheDish", "Conquest Small", "Rogue Transmission"),
                new MapMode(20, "ConquestSmall0", "MP_Tremors", "Conquest Small", "Dawnbreaker"),
                new MapMode(21, "Domination0", "MP_Abandoned", "Domination", "Zavod 311"),
                new MapMode(22, "Domination0", "MP_Damage", "Domination", "Lancang Dam"),
                new MapMode(23, "Domination0", "MP_Flooded", "Domination", "Flood Zone"),
                new MapMode(24, "Domination0", "MP_Journey", "Domination", "Golmud Railway"),
                new MapMode(25, "Domination0", "MP_Naval", "Domination", "Paracel Storm"),
                new MapMode(26, "Domination0", "MP_Prison", "Domination", "Operation Locker"),
                new MapMode(27, "Domination0", "MP_Resort", "Domination", "Hainan Resort"),
                new MapMode(28, "Domination0", "MP_Siege", "Domination", "Siege of Shanghai"),
                new MapMode(29, "Domination0", "MP_TheDish", "Domination", "Rogue Transmission"),
                new MapMode(30, "Domination0", "MP_Tremors", "Domination", "Dawnbreaker"),
                new MapMode(31, "Elimination0", "MP_Abandoned", "Defuse", "Zavod 311"),
                new MapMode(32, "Elimination0", "MP_Damage", "Defuse", "Lancang Dam"),
                new MapMode(33, "Elimination0", "MP_Flooded", "Defuse", "Flood Zone"),
                new MapMode(34, "Elimination0", "MP_Journey", "Defuse", "Golmud Railway"),
                new MapMode(35, "Elimination0", "MP_Naval", "Defuse", "Paracel Storm"),
                new MapMode(36, "Elimination0", "MP_Prison", "Defuse", "Operation Locker"),
                new MapMode(37, "Elimination0", "MP_Resort", "Defuse", "Hainan Resort"),
                new MapMode(38, "Elimination0", "MP_Siege", "Defuse", "Siege of Shanghai"),
                new MapMode(39, "Elimination0", "MP_TheDish", "Defuse", "Rogue Transmission"),
                new MapMode(40, "Obliteration", "MP_Abandoned", "Obliteration", "Zavod 311"),
                new MapMode(41, "Obliteration", "MP_Damage", "Obliteration", "Lancang Dam"),
                new MapMode(42, "Obliteration", "MP_Flooded", "Obliteration", "Flood Zone"),
                new MapMode(43, "Obliteration", "MP_Journey", "Obliteration", "Golmud Railway"),
                new MapMode(44, "Obliteration", "MP_Naval", "Obliteration", "Paracel Storm"),
                new MapMode(45, "Obliteration", "MP_Prison", "Obliteration", "Operation Locker"),
                new MapMode(46, "Obliteration", "MP_Resort", "Obliteration", "Hainan Resort"),
                new MapMode(47, "Obliteration", "MP_Siege", "Obliteration", "Siege of Shanghai"),
                new MapMode(48, "Obliteration", "MP_TheDish", "Obliteration", "Rogue Transmission"),
                new MapMode(49, "Obliteration", "MP_Tremors", "Obliteration", "Dawnbreaker"),
                new MapMode(50, "RushLarge0", "MP_Abandoned", "Rush", "Zavod 311"),
                new MapMode(51, "RushLarge0", "MP_Damage", "Rush", "Lancang Dam"),
                new MapMode(52, "RushLarge0", "MP_Flooded", "Rush", "Flood Zone"),
                new MapMode(53, "RushLarge0", "MP_Journey", "Rush", "Golmud Railway"),
                new MapMode(54, "RushLarge0", "MP_Naval", "Rush", "Paracel Storm"),
                new MapMode(55, "RushLarge0", "MP_Prison", "Rush", "Operation Locker"),
                new MapMode(56, "RushLarge0", "MP_Resort", "Rush", "Hainan Resort"),
                new MapMode(57, "RushLarge0", "MP_Siege", "Rush", "Siege of Shanghai"),
                new MapMode(58, "RushLarge0", "MP_TheDish", "Rush", "Rogue Transmission"),
                new MapMode(59, "RushLarge0", "MP_Tremors", "Rush", "Dawnbreaker"),
                new MapMode(60, "SquadDeathMatch0", "MP_Abandoned", "Squad Deathmatch", "Zavod 311"),
                new MapMode(61, "SquadDeathMatch0", "MP_Damage", "Squad Deathmatch", "Lancang Dam"),
                new MapMode(62, "SquadDeathMatch0", "MP_Flooded", "Squad Deathmatch", "Flood Zone"),
                new MapMode(63, "SquadDeathMatch0", "MP_Journey", "Squad Deathmatch", "Golmud Railway"),
                new MapMode(64, "SquadDeathMatch0", "MP_Naval", "Squad Deathmatch", "Paracel Storm"),
                new MapMode(65, "SquadDeathMatch0", "MP_Prison", "Squad Deathmatch", "Operation Locker"),
                new MapMode(66, "SquadDeathMatch0", "MP_Resort", "Squad Deathmatch", "Hainan Resort"),
                new MapMode(67, "SquadDeathMatch0", "MP_Siege", "Squad Deathmatch", "Siege of Shanghai"),
                new MapMode(68, "SquadDeathMatch0", "MP_TheDish", "Squad Deathmatch", "Rogue Transmission"),
                new MapMode(69, "SquadDeathMatch0", "MP_Tremors", "Squad Deathmatch", "Dawnbreaker"),
                new MapMode(70, "TeamDeathMatch0", "MP_Abandoned", "Team Deathmatch", "Zavod 311"),
                new MapMode(71, "TeamDeathMatch0", "MP_Damage", "Team Deathmatch", "Lancang Dam"),
                new MapMode(72, "TeamDeathMatch0", "MP_Flooded", "Team Deathmatch", "Flood Zone"),
                new MapMode(73, "TeamDeathMatch0", "MP_Journey", "Team Deathmatch", "Golmud Railway"),
                new MapMode(74, "TeamDeathMatch0", "MP_Naval", "Team Deathmatch", "Paracel Storm"),
                new MapMode(75, "TeamDeathMatch0", "MP_Prison", "Team Deathmatch", "Operation Locker"),
                new MapMode(76, "TeamDeathMatch0", "MP_Resort", "Team Deathmatch", "Hainan Resort"),
                new MapMode(77, "TeamDeathMatch0", "MP_Siege", "Team Deathmatch", "Siege of Shanghai"),
                new MapMode(78, "TeamDeathMatch0", "MP_TheDish", "Team Deathmatch", "Rogue Transmission"),
                new MapMode(79, "TeamDeathMatch0", "MP_Tremors", "Team Deathmatch", "Dawnbreaker"),
                new MapMode(80, "ConquestLarge0", "XP1_001", "Conquest Large", "Silk Road"),
                new MapMode(81, "ConquestLarge0", "XP1_002", "Conquest Large", "Altai Range"),
                new MapMode(82, "ConquestLarge0", "XP1_003", "Conquest Large", "Guilin Peaks"),
                new MapMode(83, "ConquestLarge0", "XP1_004", "Conquest Large", "Dragon Pass"),
                new MapMode(84, "ConquestSmall0", "XP1_001", "Conquest Small", "Silk Road"),
                new MapMode(85, "ConquestSmall0", "XP1_002", "Conquest Small", "Altai Range"),
                new MapMode(86, "ConquestSmall0", "XP1_003", "Conquest Small", "Guilin Peaks"),
                new MapMode(87, "ConquestSmall0", "XP1_004", "Conquest Small", "Dragon Pass"),
                new MapMode(88, "Domination0", "XP1_001", "Domination", "Silk Road"),
                new MapMode(89, "Domination0", "XP1_002", "Domination", "Altai Range"),
                new MapMode(90, "Domination0", "XP1_003", "Domination", "Guilin Peaks"),
                new MapMode(91, "Domination0", "XP1_004", "Domination", "Dragon Pass"),
                new MapMode(92, "Elimination0", "XP1_001", "Defuse", "Silk Road"),
                new MapMode(93, "Elimination0", "XP1_002", "Defuse", "Altai Range"),
                new MapMode(94, "Elimination0", "XP1_003", "Defuse", "Guilin Peaks"),
                new MapMode(95, "Elimination0", "XP1_004", "Defuse", "Dragon Pass"),
                new MapMode(96, "Obliteration", "XP1_001", "Obliteration", "Silk Road"),
                new MapMode(97, "Obliteration", "XP1_002", "Obliteration", "Altai Range"),
                new MapMode(98, "Obliteration", "XP1_003", "Obliteration", "Guilin Peaks"),
                new MapMode(99, "Obliteration", "XP1_004", "Obliteration", "Dragon Pass"),
                new MapMode(100, "RushLarge0", "XP1_001", "Rush", "Silk Road"),
                new MapMode(101, "RushLarge0", "XP1_002", "Rush", "Altai Range"),
                new MapMode(102, "RushLarge0", "XP1_003", "Rush", "Guilin Peaks"),
                new MapMode(103, "RushLarge0", "XP1_004", "Rush", "Dragon Pass"),
                new MapMode(104, "SquadDeathMatch0", "XP1_001", "Squad Deathmatch", "Silk Road"),
                new MapMode(105, "SquadDeathMatch0", "XP1_002", "Squad Deathmatch", "Altai Range"),
                new MapMode(106, "SquadDeathMatch0", "XP1_003", "Squad Deathmatch", "Guilin Peaks"),
                new MapMode(107, "SquadDeathMatch0", "XP1_004", "Squad Deathmatch", "Dragon Pass"),
                new MapMode(108, "TeamDeathMatch0", "XP1_001", "Team Deathmatch", "Silk Road"),
                new MapMode(109, "TeamDeathMatch0", "XP1_002", "Team Deathmatch", "Altai Range"),
                new MapMode(110, "TeamDeathMatch0", "XP1_003", "Team Deathmatch", "Guilin Peaks"),
                new MapMode(111, "TeamDeathMatch0", "XP1_004", "Team Deathmatch", "Dragon Pass"),
                new MapMode(112, "AirSuperiority0", "XP1_001", "Air Superiority", "Silk Road"),
                new MapMode(113, "AirSuperiority0", "XP1_002", "Air Superiority", "Altai Range"),
                new MapMode(114, "AirSuperiority0", "XP1_003", "Air Superiority", "Guilin Peaks"),
                new MapMode(115, "AirSuperiority0", "XP1_004", "Air Superiority", "Dragon Pass"),
                new MapMode(116, "ConquestLarge0", "XP0_Caspian", "Conquest Large", "Caspian Border 2014"),
                new MapMode(117, "ConquestLarge0", "XP0_Firestorm", "Conquest Large", "Operation Firestorm 2014"),
                new MapMode(118, "ConquestLarge0", "XP0_Metro", "Conquest Large", "Operation Metro 2014"),
                new MapMode(119, "ConquestLarge0", "XP0_Oman", "Conquest Large", "Gulf of Oman 2014"),
                new MapMode(120, "ConquestSmall0", "XP0_Caspian", "Conquest Small", "Caspian Border 2014"),
                new MapMode(121, "ConquestSmall0", "XP0_Firestorm", "Conquest Small", "Operation Firestorm 2014"),
                new MapMode(122, "ConquestSmall0", "XP0_Metro", "Conquest Small", "Operation Metro 2014"),
                new MapMode(123, "ConquestSmall0", "XP0_Oman", "Conquest Small", "Gulf of Oman 2014"),
                new MapMode(124, "Domination0", "XP0_Caspian", "Domination", "Caspian Border 2014"),
                new MapMode(125, "Domination0", "XP0_Firestorm", "Domination", "Operation Firestorm 2014"),
                new MapMode(126, "Domination0", "XP0_Metro", "Domination", "Operation Metro 2014"),
                new MapMode(127, "Domination0", "XP0_Oman", "Domination", "Gulf of Oman 2014"),
                new MapMode(128, "Elimination0", "XP0_Caspian", "Defuse", "Caspian Border 2014"),
                new MapMode(129, "Elimination0", "XP0_Firestorm", "Defuse", "Operation Firestorm 2014"),
                new MapMode(130, "Elimination0", "XP0_Metro", "Defuse", "Operation Metro 2014"),
                new MapMode(131, "Elimination0", "XP0_Oman", "Defuse", "Gulf of Oman 2014"),
                new MapMode(132, "Obliteration", "XP0_Caspian", "Obliteration", "Caspian Border 2014"),
                new MapMode(133, "Obliteration", "XP0_Firestorm", "Obliteration", "Operation Firestorm 2014"),
                new MapMode(134, "Obliteration", "XP0_Metro", "Obliteration", "Operation Metro 2014"),
                new MapMode(135, "Obliteration", "XP0_Oman", "Obliteration", "Gulf of Oman 2014"),
                new MapMode(136, "RushLarge0", "XP0_Caspian", "Rush", "Caspian Border 2014"),
                new MapMode(137, "RushLarge0", "XP0_Firestorm", "Rush", "Operation Firestorm 2014"),
                new MapMode(138, "RushLarge0", "XP0_Metro", "Rush", "Operation Metro 2014"),
                new MapMode(139, "RushLarge0", "XP0_Oman", "Rush", "Gulf of Oman 2014"),
                new MapMode(140, "SquadDeathMatch0", "XP0_Caspian", "Squad Deathmatch", "Caspian Border 2014"),
                new MapMode(141, "SquadDeathMatch0", "XP0_Firestorm", "Squad Deathmatch", "Operation Firestorm 2014"),
                new MapMode(142, "SquadDeathMatch0", "XP0_Metro", "Squad Deathmatch", "Operation Metro 2014"),
                new MapMode(143, "SquadDeathMatch0", "XP0_Oman", "Squad Deathmatch", "Gulf of Oman 2014"),
                new MapMode(144, "TeamDeathMatch0", "XP0_Caspian", "Team Deathmatch", "Caspian Border 2014"),
                new MapMode(145, "TeamDeathMatch0", "XP0_Firestorm", "Team Deathmatch", "Operation Firestorm 2014"),
                new MapMode(146, "TeamDeathMatch0", "XP0_Metro", "Team Deathmatch", "Operation Metro 2014"),
                new MapMode(147, "TeamDeathMatch0", "XP0_Oman", "Team Deathmatch", "Gulf of Oman 2014"),
                new MapMode(148, "CaptureTheFlag0", "XP0_Caspian", "CTF", "Caspian Border 2014"),
                new MapMode(149, "CaptureTheFlag0", "XP0_Firestorm", "CTF", "Operation Firestorm 2014"),
                new MapMode(150, "CaptureTheFlag0", "XP0_Metro", "CTF", "Operation Metro 2014"),
                new MapMode(151, "CaptureTheFlag0", "XP0_Oman", "CTF", "Gulf of Oman 2014"),
                new MapMode(152, "ConquestLarge0", "XP2_001", "Conquest Large", "Lost Islands"),
                new MapMode(153, "ConquestLarge0", "XP2_002", "Conquest Large", "Nansha Strike"),
                new MapMode(154, "ConquestLarge0", "XP2_003", "Conquest Large", "Wavebreaker"),
                new MapMode(155, "ConquestLarge0", "XP2_004", "Conquest Large", "Operation Mortar"),
                new MapMode(156, "ConquestSmall0", "XP2_001", "Conquest Small", "Lost Islands"),
                new MapMode(157, "ConquestSmall0", "XP2_002", "Conquest Small", "Nansha Strike"),
                new MapMode(158, "ConquestSmall0", "XP2_003", "Conquest Small", "Wavebreaker"),
                new MapMode(159, "ConquestSmall0", "XP2_004", "Conquest Small", "Operation Mortar"),
                new MapMode(160, "Domination0", "XP2_001", "Domination", "Lost Islands"),
                new MapMode(161, "Domination0", "XP2_002", "Domination", "Nansha Strike"),
                new MapMode(162, "Domination0", "XP2_003", "Domination", "Wavebreaker"),
                new MapMode(163, "Domination0", "XP2_004", "Domination", "Operation Mortar"),
                new MapMode(164, "Elimination0", "XP2_001", "Defuse", "Lost Islands"),
                new MapMode(165, "Elimination0", "XP2_002", "Defuse", "Nansha Strike"),
                new MapMode(166, "Elimination0", "XP2_003", "Defuse", "Wavebreaker"),
                new MapMode(167, "Elimination0", "XP2_004", "Defuse", "Operation Mortar"),
                new MapMode(168, "Obliteration", "XP2_001", "Obliteration", "Lost Islands"),
                new MapMode(169, "Obliteration", "XP2_002", "Obliteration", "Nansha Strike"),
                new MapMode(170, "Obliteration", "XP2_003", "Obliteration", "Wavebreaker"),
                new MapMode(171, "Obliteration", "XP2_004", "Obliteration", "Operation Mortar"),
                new MapMode(172, "RushLarge0", "XP2_001", "Rush", "Lost Islands"),
                new MapMode(173, "RushLarge0", "XP2_002", "Rush", "Nansha Strike"),
                new MapMode(174, "RushLarge0", "XP2_003", "Rush", "Wavebreaker"),
                new MapMode(175, "RushLarge0", "XP2_004", "Rush", "Operation Mortar"),
                new MapMode(176, "SquadDeathMatch0", "XP2_001", "Squad Deathmatch", "Lost Islands"),
                new MapMode(177, "SquadDeathMatch0", "XP2_002", "Squad Deathmatch", "Nansha Strike"),
                new MapMode(178, "SquadDeathMatch0", "XP2_003", "Squad Deathmatch", "Wavebreaker"),
                new MapMode(179, "SquadDeathMatch0", "XP2_004", "Squad Deathmatch", "Operation Mortar"),
                new MapMode(180, "TeamDeathMatch0", "XP2_001", "Team Deathmatch", "Lost Islands"),
                new MapMode(181, "TeamDeathMatch0", "XP2_002", "Team Deathmatch", "Nansha Strike"),
                new MapMode(182, "TeamDeathMatch0", "XP2_003", "Team Deathmatch", "Wavebreaker"),
                new MapMode(183, "TeamDeathMatch0", "XP2_004", "Team Deathmatch", "Operation Mortar"),
                new MapMode(184, "CarrierAssaultLarge0", "XP2_001", "Carrier Assault Large", "Lost Islands"),
                new MapMode(185, "CarrierAssaultLarge0", "XP2_002", "Carrier Assault Large", "Nansha Strike"),
                new MapMode(186, "CarrierAssaultLarge0", "XP2_003", "Carrier Assault Large", "Wavebreaker"),
                new MapMode(187, "CarrierAssaultLarge0", "XP2_004", "Carrier Assault Large", "Operation Mortar"),
                new MapMode(188, "CarrierAssaultSmall0", "XP2_001", "Carrier Assault Small", "Lost Islands"),
                new MapMode(189, "CarrierAssaultSmall0", "XP2_002", "Carrier Assault Small", "Nansha Strike"),
                new MapMode(190, "CarrierAssaultSmall0", "XP2_003", "Carrier Assault Small", "Wavebreaker"),
                new MapMode(191, "CarrierAssaultSmall0", "XP2_004", "Carrier Assault Small", "Operation Mortar"),
                new MapMode(192, "ConquestLarge0", "XP3_MarketPl", "Conquest Large", "Pearl Market"),
                new MapMode(193, "ConquestLarge0", "XP3_Prpganda", "Conquest Large", "Propaganda"),
                new MapMode(194, "ConquestLarge0", "XP3_UrbanGdn", "Conquest Large", "Lumphini Garden"),
                new MapMode(195, "ConquestLarge0", "XP3_WtrFront", "Conquest Large", "Sunken Dragon"),
                new MapMode(196, "ConquestSmall0", "XP3_MarketPl", "Conquest Small", "Pearl Market"),
                new MapMode(197, "ConquestSmall0", "XP3_Prpganda", "Conquest Small", "Propaganda"),
                new MapMode(198, "ConquestSmall0", "XP3_UrbanGdn", "Conquest Small", "Lumphini Garden"),
                new MapMode(199, "ConquestSmall0", "XP3_WtrFront", "Conquest Small", "Sunken Dragon"),
                new MapMode(200, "Domination0", "XP3_MarketPl", "Domination", "Pearl Market"),
                new MapMode(201, "Domination0", "XP3_Prpganda", "Domination", "Propaganda"),
                new MapMode(202, "Domination0", "XP3_UrbanGdn", "Domination", "Lumphini Garden"),
                new MapMode(203, "Domination0", "XP3_WtrFront", "Domination", "Sunken Dragon"),
                new MapMode(204, "Elimination0", "XP3_MarketPl", "Defuse", "Pearl Market"),
                new MapMode(205, "Elimination0", "XP3_Prpganda", "Defuse", "Propaganda"),
                new MapMode(206, "Elimination0", "XP3_UrbanGdn", "Defuse", "Lumphini Garden"),
                new MapMode(207, "Elimination0", "XP3_WtrFront", "Defuse", "Sunken Dragon"),
                new MapMode(208, "Obliteration", "XP3_MarketPl", "Obliteration", "Pearl Market"),
                new MapMode(209, "Obliteration", "XP3_Prpganda", "Obliteration", "Propaganda"),
                new MapMode(210, "Obliteration", "XP3_UrbanGdn", "Obliteration", "Lumphini Garden"),
                new MapMode(211, "Obliteration", "XP3_WtrFront", "Obliteration", "Sunken Dragon"),
                new MapMode(212, "RushLarge0", "XP3_MarketPl", "Rush", "Pearl Market"),
                new MapMode(213, "RushLarge0", "XP3_Prpganda", "Rush", "Propaganda"),
                new MapMode(214, "RushLarge0", "XP3_UrbanGdn", "Rush", "Lumphini Garden"),
                new MapMode(215, "RushLarge0", "XP3_WtrFront", "Rush", "Sunken Dragon"),
                new MapMode(216, "SquadDeathMatch0", "XP3_MarketPl", "Squad Deathmatch", "Pearl Market"),
                new MapMode(217, "SquadDeathMatch0", "XP3_Prpganda", "Squad Deathmatch", "Propaganda"),
                new MapMode(218, "SquadDeathMatch0", "XP3_UrbanGdn", "Squad Deathmatch", "Lumphini Garden"),
                new MapMode(219, "SquadDeathMatch0", "XP3_WtrFront", "Squad Deathmatch", "Sunken Dragon"),
                new MapMode(220, "TeamDeathMatch0", "XP3_MarketPl", "Team Deathmatch", "Pearl Market"),
                new MapMode(221, "TeamDeathMatch0", "XP3_Prpganda", "Team Deathmatch", "Propaganda"),
                new MapMode(222, "TeamDeathMatch0", "XP3_UrbanGdn", "Team Deathmatch", "Lumphini Garden"),
                new MapMode(223, "TeamDeathMatch0", "XP3_WtrFront", "Team Deathmatch", "Sunken Dragon"),
                new MapMode(224, "CaptureTheFlag0", "XP3_MarketPl", "CTF", "Pearl Market"),
                new MapMode(225, "CaptureTheFlag0", "XP3_Prpganda", "CTF", "Propaganda"),
                new MapMode(226, "CaptureTheFlag0", "XP3_UrbanGdn", "CTF", "Lumphini Garden"),
                new MapMode(227, "CaptureTheFlag0", "XP3_WtrFront", "CTF", "Sunken Dragon"),
                new MapMode(228, "Chainlink0", "XP3_MarketPl", "Chain Link", "Pearl Market"),
                new MapMode(229, "Chainlink0", "XP3_Prpganda", "Chain Link", "Propaganda"),
                new MapMode(230, "Chainlink0", "XP3_UrbanGdn", "Chain Link", "Lumphini Garden"),
                new MapMode(231, "Chainlink0", "XP3_WtrFront", "Chain Link", "Sunken Dragon"),
                new MapMode(232, "ConquestLarge0", "XP4_Arctic", "Conquest Large", "Operation Whiteout"),
                new MapMode(233, "ConquestLarge0", "XP4_SubBase", "Conquest Large", "Hammerhead"),
                new MapMode(234, "ConquestLarge0", "XP4_Titan", "Conquest Large", "Hangar 21"),
                new MapMode(235, "ConquestLarge0", "XP4_WlkrFtry", "Conquest Large", "Giants Of Karelia"),
                new MapMode(236, "ConquestSmall0", "XP4_Arctic", "Conquest Small", "Operation Whiteout"),
                new MapMode(237, "ConquestSmall0", "XP4_SubBase", "Conquest Small", "Hammerhead"),
                new MapMode(238, "ConquestSmall0", "XP4_Titan", "Conquest Small", "Hangar 21"),
                new MapMode(239, "ConquestSmall0", "XP4_WlkrFtry", "Conquest Small", "Giants Of Karelia"),
                new MapMode(240, "Domination0", "XP4_Arctic", "Domination", "Operation Whiteout"),
                new MapMode(241, "Domination0", "XP4_SubBase", "Domination", "Hammerhead"),
                new MapMode(242, "Domination0", "XP4_Titan", "Domination", "Hangar 21"),
                new MapMode(243, "Domination0", "XP4_WlkrFtry", "Domination", "Giants Of Karelia"),
                new MapMode(244, "Elimination0", "XP4_Arctic", "Defuse", "Operation Whiteout"),
                new MapMode(245, "Elimination0", "XP4_SubBase", "Defuse", "Hammerhead"),
                new MapMode(246, "Elimination0", "XP4_Titan", "Defuse", "Hangar 21"),
                new MapMode(247, "Elimination0", "XP4_WlkrFtry", "Defuse", "Giants Of Karelia"),
                new MapMode(248, "Obliteration", "XP4_Arctic", "Obliteration", "Operation Whiteout"),
                new MapMode(249, "Obliteration", "XP4_SubBase", "Obliteration", "Hammerhead"),
                new MapMode(250, "Obliteration", "XP4_Titan", "Obliteration", "Hangar 21"),
                new MapMode(251, "Obliteration", "XP4_WlkrFtry", "Obliteration", "Giants Of Karelia"),
                new MapMode(252, "RushLarge0", "XP4_Arctic", "Rush", "Operation Whiteout"),
                new MapMode(253, "RushLarge0", "XP4_SubBase", "Rush", "Hammerhead"),
                new MapMode(254, "RushLarge0", "XP4_Titan", "Rush", "Hangar 21"),
                new MapMode(255, "RushLarge0", "XP4_WlkrFtry", "Rush", "Giants Of Karelia"),
                new MapMode(256, "SquadDeathMatch0", "XP4_Arctic", "Squad Deathmatch", "Operation Whiteout"),
                new MapMode(257, "SquadDeathMatch0", "XP4_SubBase", "Squad Deathmatch", "Hammerhead"),
                new MapMode(258, "SquadDeathMatch0", "XP4_Titan", "Squad Deathmatch", "Hangar 21"),
                new MapMode(259, "SquadDeathMatch0", "XP4_WlkrFtry", "Squad Deathmatch", "Giants Of Karelia"),
                new MapMode(260, "TeamDeathMatch0", "XP4_Arctic", "Team Deathmatch", "Operation Whiteout"),
                new MapMode(261, "TeamDeathMatch0", "XP4_SubBase", "Team Deathmatch", "Hammerhead"),
                new MapMode(262, "TeamDeathMatch0", "XP4_Titan", "Team Deathmatch", "Hangar 21"),
                new MapMode(263, "TeamDeathMatch0", "XP4_WlkrFtry", "Team Deathmatch", "Giants Of Karelia"),
                new MapMode(264, "CaptureTheFlag0", "XP4_Arctic", "CTF", "Operation Whiteout"),
                new MapMode(265, "CaptureTheFlag0", "XP4_SubBase", "CTF", "Hammerhead"),
                new MapMode(266, "CaptureTheFlag0", "XP4_Titan", "CTF", "Hangar 21"),
                new MapMode(267, "CaptureTheFlag0", "XP4_WlkrFtry", "CTF", "Giants Of Karelia"),
                new MapMode(268, "SquadObliteration0", "MP_Abandoned", "Squad Obliteration", "Zavod 311"),
                new MapMode(269, "SquadObliteration0", "MP_Journey", "Squad Obliteration", "Golmud Railway"),
                new MapMode(270, "SquadObliteration0", "MP_Naval", "Squad Obliteration", "Paracel Storm"),
                new MapMode(271, "SquadObliteration0", "MP_Prison", "Squad Obliteration", "Operation Locker"),
                new MapMode(272, "SquadObliteration0", "MP_Resort", "Squad Obliteration", "Hainan Resort"),
                new MapMode(273, "SquadObliteration0", "MP_Siege", "Squad Obliteration", "Siege of Shanghai"),
                new MapMode(274, "SquadObliteration0", "MP_Tremors", "Squad Obliteration", "Dawnbreaker"),
                new MapMode(275, "ConquestLarge0", "XP5_Night_01", "Conquest Large", "Zavod:Graveyard Shift"),
                new MapMode(276, "ConquestSmall0", "XP5_Night_01", "Conquest Small", "Zavod:Graveyard Shift"),
                new MapMode(277, "Domination0", "XP5_Night_01", "Domination", "Zavod:Graveyard Shift"),
                new MapMode(278, "Obliteration", "XP5_Night_01", "Obliteration", "Zavod:Graveyard Shift"),
                new MapMode(279, "RushLarge0", "XP5_Night_01", "Rush", "Zavod:Graveyard Shift"),
                new MapMode(280, "TeamDeathMatch0", "XP5_Night_01", "Team Deathmatch", "Zavod:Graveyard Shift"),
                new MapMode(281, "ConquestLarge0", "XP6_CMP", "Conquest Large", "Operation Outbreak"),
                new MapMode(282, "ConquestSmall0", "XP6_CMP", "Conquest Small", "Operation Outbreak"),
                new MapMode(283, "Domination0", "XP6_CMP", "Domination", "Operation Outbreak"),
                new MapMode(284, "Obliteration", "XP6_CMP", "Obliteration", "Operation Outbreak"),
                new MapMode(285, "RushLarge0", "XP6_CMP", "Rush", "Operation Outbreak"),
                new MapMode(286, "SquadDeathMatch0", "XP6_CMP", "Squad Deathmatch", "Operation Outbreak"),
                new MapMode(287, "SquadDeathMatch1", "XP6_CMP", "Squad Deathmatch", "Operation Outbreak v2"),
                new MapMode(288, "TeamDeathMatch0", "XP6_CMP", "Team Deathmatch", "Operation Outbreak"),
                new MapMode(289, "TeamDeathMatch1", "XP6_CMP", "Team Deathmatch", "Operation Outbreak v2"),
                new MapMode(290, "CaptureTheFlag0", "XP6_CMP", "CTF", "Operation Outbreak"),
                new MapMode(291, "Chainlink0", "XP6_CMP", "Chain Link", "Operation Outbreak"),
                new MapMode(292, "ConquestLarge0", "XP7_Valley", "Conquest Large", "Dragon Valley 2015"),
                new MapMode(293, "ConquestSmall0", "XP7_Valley", "Conquest Small", "Dragon Valley 2015"),
                new MapMode(294, "Domination0", "XP7_Valley", "Domination", "Dragon Valley 2015"),
                new MapMode(295, "Obliteration", "XP7_Valley", "Obliteration", "Dragon Valley 2015"),
                new MapMode(296, "RushLarge0", "XP7_Valley", "Rush", "Dragon Valley 2015"),
                new MapMode(297, "SquadDeathMatch0", "XP7_Valley", "Squad Deathmatch", "Dragon Valley 2015"),
                new MapMode(298, "TeamDeathMatch0", "XP7_Valley", "Team Deathmatch", "Dragon Valley 2015"),
                new MapMode(299, "GunMaster0", "XP7_Valley", "Gun Master", "Dragon Valley 2015"),
                new MapMode(300, "AirSuperiority0", "XP7_Valley", "Air Superiority", "Dragon Valley 2015")
            };
        }

        private void PopulateWarsawRCONCodes()
        {
            //Load in all knowns WARSAW to RCON mappings for use with the autoadmin
            _WarsawRCONMappings.Clear();
            _RCONWarsawMappings.Clear();

            //DMRs
            _WarsawRCONMappings["1915356177"] = (new String[] { "U_M39EBR" }).ToList();
            _WarsawRCONMappings["4092888892"] = (new String[] { "U_SVD12" }).ToList();
            _WarsawRCONMappings["3860123089"] = (new String[] { "U_GalilACE53" }).ToList();
            _WarsawRCONMappings["1906761969"] = (new String[] { "U_QBU88" }).ToList();
            _WarsawRCONMappings["3072292273"] = (new String[] { "U_SCAR-HSV" }).ToList();
            _WarsawRCONMappings["2144050545"] = (new String[] { "U_SKS" }).ToList();
            _WarsawRCONMappings["1894217457"] = (new String[] { "U_RFB" }).ToList();
            _WarsawRCONMappings["408290737"] = (new String[] { "U_MK11" }).ToList();
            _WarsawRCONMappings["2759849572"] = (new String[] { "U_SR338" }).ToList();

            //Snipers
            _WarsawRCONMappings["2853300518"] = (new String[] { "U_GOL" }).ToList();
            _WarsawRCONMappings["2897869395"] = (new String[] { "U_M98B" }).ToList();
            _WarsawRCONMappings["2967613745"] = (new String[] { "U_Scout" }).ToList();
            _WarsawRCONMappings["1079830129"] = (new String[] { "U_FY-JS" }).ToList();
            _WarsawRCONMappings["1596514833"] = (new String[] { "U_SV98" }).ToList();
            _WarsawRCONMappings["3458855537"] = (new String[] { "U_CS-LR4" }).ToList();
            _WarsawRCONMappings["4135125553"] = (new String[] { "U_JNG90" }).ToList();
            _WarsawRCONMappings["1834910833"] = (new String[] { "U_M40A5" }).ToList();
            _WarsawRCONMappings["388555399"] = (new String[] { "U_L96A1" }).ToList();
            _WarsawRCONMappings["3555293285"] = (new String[] { "U_CS5" }).ToList();
            _WarsawRCONMappings["3081643377"] = (new String[] { "U_SRS" }).ToList();
            _WarsawRCONMappings["1710440049"] = (new String[] { "U_M200" }).ToList();

            //PDWs
            _WarsawRCONMappings["1020126577"] = (new String[] { "U_P90" }).ToList();
            _WarsawRCONMappings["2021343793"] = (new String[] { "U_MX4" }).ToList();
            _WarsawRCONMappings["763058951"] = (new String[] { "U_MP7" }).ToList();
            _WarsawRCONMappings["3382662737"] = (new String[] { "U_PP2000" }).ToList();
            _WarsawRCONMappings["1030797713"] = (new String[] { "U_CBJ-MS" }).ToList();
            _WarsawRCONMappings["2128008177"] = (new String[] { "U_UMP45" }).ToList();
            _WarsawRCONMappings["2665548081"] = (new String[] { "U_Scorpion" }).ToList();
            _WarsawRCONMappings["4227814065"] = (new String[] { "U_UMP9" }).ToList();
            _WarsawRCONMappings["4208515505"] = (new String[] { "U_MagpulPDR" }).ToList();
            _WarsawRCONMappings["3204230182"] = (new String[] { "U_ASVal" }).ToList();
            _WarsawRCONMappings["3188912241"] = (new String[] { "U_JS2" }).ToList();
            _WarsawRCONMappings["1689098981"] = (new String[] { "U_MPX" }).ToList();
            _WarsawRCONMappings["821324708"] = (new String[] { "U_SR2" }).ToList();
            _WarsawRCONMappings["2203062595"] = (new String[] { "U_Groza-4" }).ToList();

            //Assault Rifles
            _WarsawRCONMappings["3059253169"] = (new String[] { "U_GalilACE23" }).ToList();
            _WarsawRCONMappings["2643258020"] = (new String[] { "U_AR160" }).ToList();
            _WarsawRCONMappings["4279753681"] = (new String[] { "U_M416" }).ToList();
            _WarsawRCONMappings["2829366246"] = (new String[] { "U_F2000" }).ToList();
            _WarsawRCONMappings["319908497"] = (new String[] { "U_SteyrAug" }).ToList();
            _WarsawRCONMappings["2815752497"] = (new String[] { "U_FAMAS" }).ToList();
            _WarsawRCONMappings["2826786481"] = (new String[] { "U_CZ805" }).ToList();
            _WarsawRCONMappings["819903973"] = (new String[] { "U_Bulldog" }).ToList();
            _WarsawRCONMappings["1687010979"] = (new String[] { "U_AN94" }).ToList();
            _WarsawRCONMappings["234564305"] = (new String[] { "U_QBZ951" }).ToList();
            _WarsawRCONMappings["4242111601"] = (new String[] { "U_SAR21" }).ToList();
            _WarsawRCONMappings["669091281"] = (new String[] { "U_AEK971" }).ToList();
            _WarsawRCONMappings["174491409"] = (new String[] { "U_SCAR-H" }).ToList();
            _WarsawRCONMappings["821324709"] = (new String[] { "U_L85A2" }).ToList();
            _WarsawRCONMappings["3590299697"] = (new String[] { "U_AK12" }).ToList();
            _WarsawRCONMappings["3119417649"] = (new String[] { "U_M16A4" }).ToList();

            //Carbines
            _WarsawRCONMappings["1896957361"] = (new String[] { "U_SG553LB" }).ToList();
            _WarsawRCONMappings["2978429873"] = (new String[] { "U_G36C" }).ToList();
            _WarsawRCONMappings["3313614225"] = (new String[] { "U_AK5C" }).ToList();
            _WarsawRCONMappings["2864846705"] = (new String[] { "U_AKU12" }).ToList();
            _WarsawRCONMappings["2830105186"] = (new String[] { "dlSHTR" }).ToList();
            _WarsawRCONMappings["1987438087"] = (new String[] { "U_MTAR21" }).ToList();
            _WarsawRCONMappings["3192695217"] = (new String[] { "U_MTAR21" }).ToList();
            _WarsawRCONMappings["2152664305"] = (new String[] { "U_Type95B" }).ToList();
            _WarsawRCONMappings["326957379"] = (new String[] { "U_Groza-1" }).ToList();
            _WarsawRCONMappings["3448559030"] = (new String[] { "U_GalilACE" }).ToList();
            _WarsawRCONMappings["2082703729"] = (new String[] { "U_GalilACE52" }).ToList();
            _WarsawRCONMappings["458988977"] = (new String[] { "U_ACR" }).ToList();
            _WarsawRCONMappings["2713563633"] = (new String[] { "U_A91" }).ToList();
            _WarsawRCONMappings["3192695217"] = (new String[] { "U_M4A1" }).ToList();

            //LMGs
            _WarsawRCONMappings["3852069478"] = (new String[] { "U_M60E4" }).ToList();
            _WarsawRCONMappings["1321048617"] = (new String[] { "U_RPK-74" }).ToList();
            _WarsawRCONMappings["2572144625"] = (new String[] { "U_M240" }).ToList();
            _WarsawRCONMappings["2749423953"] = (new String[] { "U_M249" }).ToList();
            _WarsawRCONMappings["1810379907"] = (new String[] { "U_L86A1" }).ToList();
            _WarsawRCONMappings["3900816465"] = (new String[] { "U_LSAT" }).ToList();
            _WarsawRCONMappings["3000062065"] = (new String[] { "U_Pecheneg" }).ToList();
            _WarsawRCONMappings["2005518564"] = (new String[] { "U_AWS" }).ToList();
            _WarsawRCONMappings["2048507580"] = (new String[] { "U_RPK12" }).ToList();
            _WarsawRCONMappings["302761745"] = (new String[] { "U_Type88" }).ToList();
            _WarsawRCONMappings["2403214513"] = (new String[] { "U_MG4" }).ToList();
            _WarsawRCONMappings["4226187761"] = (new String[] { "U_QBB95" }).ToList();
            _WarsawRCONMappings["3179658801"] = (new String[] { "U_Ultimax" }).ToList();

            //Handguns
            _WarsawRCONMappings["3942150929"] = (new String[] { "U_FN57" }).ToList();
            _WarsawRCONMappings["335786382"] = (new String[] { "U_SaddlegunSnp" }).ToList();
            _WarsawRCONMappings["3730491953"] = (new String[] { "U_MP443" }).ToList();
            _WarsawRCONMappings["3300350865"] = (new String[] { "U_Taurus44" }).ToList();
            _WarsawRCONMappings["1276385329"] = (new String[] { "U_M93R" }).ToList();
            _WarsawRCONMappings["1518880753"] = (new String[] { "U_CZ75" }).ToList();
            _WarsawRCONMappings["264887569"] = (new String[] { "U_M9" }).ToList();
            _WarsawRCONMappings["944904529"] = (new String[] { "U_P226" }).ToList();
            _WarsawRCONMappings["3537147505"] = (new String[] { "U_QSZ92" }).ToList();
            _WarsawRCONMappings["1322096241"] = (new String[] { "U_HK45C" }).ToList();
            _WarsawRCONMappings["1715838468"] = (new String[] { "U_SW40" }).ToList();
            _WarsawRCONMappings["3430469957"] = (new String[] { "U_Unica6" }).ToList();
            _WarsawRCONMappings["908783077"] = (new String[] { "U_DesertEagle" }).ToList();
            _WarsawRCONMappings["2363034673"] = (new String[] { "U_MP412Rex" }).ToList();
            _WarsawRCONMappings["37082993"] = (new String[] { "U_Glock18" }).ToList();
            _WarsawRCONMappings["2608762737"] = (new String[] { "U_M1911" }).ToList();

            //Shotguns
            _WarsawRCONMappings["1589481582"] = (new String[] { "U_SAIGA_20K" }).ToList();
            _WarsawRCONMappings["2942558833"] = (new String[] { "U_QBS09" }).ToList();
            _WarsawRCONMappings["3528666216"] = (new String[] { "U_SteyrAug_M26_Slug", "U_SCAR-H_M26_Slug", "U_SAR21_M26_Slug", "U_QBZ951_M26_Slug", "U_M416_M26_Slug", "U_M26Mass_Slug", "U_M16A4_M26_Slug", "U_CZ805_M26_Slug", "U_AR160_M26_Slug", "U_AK12_M26_Slug", "U_AEK971_M26_Slug" }).ToList();
            _WarsawRCONMappings["1848317553"] = (new String[] { "U_M1014" }).ToList();
            _WarsawRCONMappings["2930960995"] = (new String[] { "U_870" }).ToList();
            _WarsawRCONMappings["4054082865"] = (new String[] { "U_HAWK" }).ToList();
            _WarsawRCONMappings["4292296724"] = (new String[] { "U_M26Mass_Frag" }).ToList();
            _WarsawRCONMappings["4174194330"] = (new String[] { "U_M26Mass_Flechette" }).ToList();
            _WarsawRCONMappings["94493788"] = (new String[] { "U_DBV12" }).ToList();
            _WarsawRCONMappings["3044954406"] = (new String[] { "U_DAO12" }).ToList();
            _WarsawRCONMappings["3221408826"] = (new String[] { "U_M26Mass", "U_SteyrAug_M26_Buck", "U_SCAR-H_M26_Buck", "U_SAR21_M26_Buck", "U_QBZ951_M26_Buck", "U_M416_M26_Buck", "U_M16A4_M26_Buck", "U_CZ805_M26_Buck", "U_AR160_M26_Buck", "U_AK12_M26_Buck", "U_AEK971_M26_Buck" }).ToList();
            _WarsawRCONMappings["3044954406"] = (new String[] { "U_DAO12" }).ToList();
            _WarsawRCONMappings["4204280241"] = (new String[] { "U_SPAS12" }).ToList();
            _WarsawRCONMappings["3661909297"] = (new String[] { "U_SerbuShorty" }).ToList();
            _WarsawRCONMappings["623014897"] = (new String[] { "U_UTAS" }).ToList();

            //Gadgets
            _WarsawRCONMappings["1364316986"] = (new String[] { "U_M224", "M224" }).ToList();
            _WarsawRCONMappings["4169380388"] = (new String[] { "U_XM25_Smoke" }).ToList();
            _WarsawRCONMappings["3042980396"] = (new String[] { "UCAV" }).ToList();
            _WarsawRCONMappings["2698261753"] = (new String[] { "U_SLAM" }).ToList();
            _WarsawRCONMappings["3645048844"] = (new String[] { "AA Mine" }).ToList();
            _WarsawRCONMappings["4077480573"] = (new String[] { "U_Claymore" }).ToList();
            _WarsawRCONMappings["3398724484"] = (new String[] { "U_Claymore_Recon" }).ToList();
            _WarsawRCONMappings["3076304839"] = (new String[] { "U_C4" }).ToList();
            _WarsawRCONMappings["2375254013"] = (new String[] { "U_C4_Support" }).ToList();
            _WarsawRCONMappings["3054368924"] = (new String[] { "U_XM25_Flechette" }).ToList();
            _WarsawRCONMappings["1005841160"] = (new String[] { "U_XM25" }).ToList();
            _WarsawRCONMappings["704874518"] = (new String[] { "U_M15" }).ToList();

            //Launchers
            _WarsawRCONMappings["2880824228"] = (new String[] { "U_AN94_M320_FLASH_v1", "U_SteyrAug_M320_FLASH", "U_SCAR-H_M320_FLASH", "U_SAR21_M320_FLASH", "U_QBZ951_M320_FLASH", "U_M416_M320_FLASH", "U_M320_FLASH", "U_M16A4_M320_FLASH", "U_L85A2_M320_FLASH_V2", "U_CZ805_M320_FLASH", "U_AR160_M320_FLASH", "U_AK12_M320_FLASH", "U_AEK971_M320_FLASH" }).ToList();
            _WarsawRCONMappings["4084720679"] = (new String[] { "U_SP_M320_HE", "U_AN94_M320_HE_v1", "U_SteyrAug_M320_HE", "U_SCAR-H_M320_HE", "U_SAR21_M320_HE", "U_QBZ951_M320_HE", "U_M416_M320_HE", "U_M320_HE", "U_M16A4_M320_HE", "U_L85A2_M320_HE_V2", "U_CZ805_M320_HE", "U_AR160_M320_HE", "U_AK12_M320_HE", "U_AEK971_M320_HE" }).ToList();
            _WarsawRCONMappings["1723239682"] = (new String[] { "U_AN94_M320_SHG_v1", "U_SteyrAug_M320_SHG", "U_SCAR-H_M320_SHG", "U_SAR21_M320_SHG", "U_QBZ951_M320_SHG", "U_M416_M320_SHG", "U_M320_SHG", "U_M16A4_M320_SHG", "U_L85A2_M320_SHG_V2", "U_CZ805_M320_SHG", "U_AR160_M320_SHG", "U_AK12_M320_SHG", "U_AEK971_M320_SHG" }).ToList();
            _WarsawRCONMappings["434964836"] = (new String[] { "U_AN94_M320_3GL_v1", "U_CZ805_M320_3GL", "U_SteyrAug_M320_3GL", "U_SCAR-H_M320_3GL", "U_SAR21_M320_3GL", "U_QBZ951_M320_3GL", "U_M416_M320_3GL", "U_M320_3GL", "U_M16A4_M320_3GL", "U_L85A2_M320_3GL_V2", "U_CZ605_M320_3GL", "U_AR160_M320_3GL", "U_AK12_M320_3GL", "U_AEK971_M320_3GL" }).ToList();
            _WarsawRCONMappings["863588126"] = (new String[] { "U_AN94_M320_SMK_v1", "U_SteyrAug_M320_SMK", "U_SCAR-H_M320_SMK", "U_SAR21_M320_SMK", "U_QBZ951_M320_SMK", "U_M416_M320_SMK", "U_M320_SMK", "U_M16A4_M320_SMK", "U_L85A2_M320_SMK_V2", "U_CZ805_M320_SMK", "U_AR160_M320_SMK", "U_AK12_M320_SMK", "U_AEK971_M320_SMK" }).ToList();
            _WarsawRCONMappings["335737287"] = (new String[] { "U_AN94_M320_LVG_v1", "U_SteyrAug_M320_LVG", "U_SCAR-H_M320_LVG", "U_SAR21_M320_LVG", "U_QBZ951_M320_LVG", "U_M416_M320_LVG", "U_M320_LVG", "U_M16A4_M320_LVG", "U_L85A2_M320_LVG_V2", "U_CZ805_M320_LVG", "U_AR160_M320_LVG", "U_AK12_M320_LVG", "U_AEK971_M320_LVG" }).ToList();

            //Special
            _WarsawRCONMappings["2887915611"] = (new String[] { "U_Defib" }).ToList();
            _WarsawRCONMappings["2324320899"] = (new String[] { "U_Repairtool" }).ToList();
            _WarsawRCONMappings["312950893"] = (new String[] { "U_BallisticShield" }).ToList();
            _WarsawRCONMappings["3416970831"] = (new String[] { "Death", "EODBot" }).ToList();
            //            _WarsawRCONMappings["3881213532"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["3913003056"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["1278769027"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["3214146841"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["2930902275"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["3981629339"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["2833476239"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["2765835967"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["2358565358"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["2130832595"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["2065907307"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["4098378714"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["714992459"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["3154558973"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["3194673210"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["3332841661"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["27972285"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["2709653572"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["6240291"] = (new string[] { "Melee" }).ToList();

            //Grenades
            _WarsawRCONMappings["3767777089"] = (new String[] { "U_Grenade_RGO" }).ToList();
            _WarsawRCONMappings["3133964300"] = (new String[] { "U_M18" }).ToList();
            _WarsawRCONMappings["2842275721"] = (new String[] { "U_M34" }).ToList();
            _WarsawRCONMappings["2916285594"] = (new String[] { "U_Handflare" }).ToList();
            _WarsawRCONMappings["69312926"] = (new String[] { "U_V40" }).ToList();
            _WarsawRCONMappings["2670747868"] = (new String[] { "U_M67" }).ToList();
            _WarsawRCONMappings["1779756455"] = (new String[] { "U_Flashbang" }).ToList();

            //Rocket
            _WarsawRCONMappings["3194075724"] = (new String[] { "U_FIM92" }).ToList();
            _WarsawRCONMappings["3713498991"] = (new String[] { "U_Sa18IGLA" }).ToList();
            _WarsawRCONMappings["20932301"] = (new String[] { "U_RPG7" }).ToList();
            _WarsawRCONMappings["601919388"] = (new String[] { "U_NLAW" }).ToList();
            _WarsawRCONMappings["1359435055"] = (new String[] { "U_FGM148" }).ToList();
            _WarsawRCONMappings["3177196226"] = (new String[] { "U_SRAW" }).ToList();
            _WarsawRCONMappings["1782193877"] = (new String[] { "U_SMAW" }).ToList();

            //Populate the reverse mapping dictionary
            foreach (KeyValuePair<String, List<String>> warsawRCON in _WarsawRCONMappings)
            {
                String warsawID = warsawRCON.Key;
                List<String> matchingRCONCodes = warsawRCON.Value;

                foreach (String RCONCode in matchingRCONCodes)
                {
                    List<String> warsawIDs;
                    if (!_RCONWarsawMappings.TryGetValue(RCONCode, out warsawIDs))
                    {
                        warsawIDs = new List<String>();
                        _RCONWarsawMappings[RCONCode] = warsawIDs;
                    }
                    if (!warsawIDs.Contains(warsawID))
                    {
                        warsawIDs.Add(warsawID);
                    }
                }
            }
        }
    }
}
