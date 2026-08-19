using AetherCurrentUnlocker.Models;

namespace AetherCurrentUnlocker.Data;

/// <summary>
/// 通常エーテライトの座標をゲームデータ取得に依存せず利用するための同梱データ。
/// 座標はQuestionableのAetheryteDataと同じゲームワールド座標を使用する。
/// </summary>
internal static class AetheryteCatalog
{
    private static readonly IReadOnlyDictionary<uint, TeleportDestination[]> ByTerritory =
        new Dictionary<uint, TeleportDestination[]>
        {
            [397] = [new(71, 0, "Coerthas Western Highlands - Falcon's Nest", new(474.87585f, 217.94458f, 708.5221f))],
            [398] =
            [
                new(76, 0, "The Dravanian Forelands - Tailfeather", new(532.6771f, -48.722107f, 30.166992f)),
                new(77, 0, "The Dravanian Forelands - Anyx Trine", new(-304.12756f, -16.70868f, 32.059082f)),
            ],
            [400] =
            [
                new(78, 0, "The Churning Mists - Moghome", new(259.20496f, -37.70508f, 596.85657f)),
                new(79, 0, "The Churning Mists - Zenith", new(-584.9546f, 52.84192f, 313.43542f)),
            ],
            [401] =
            [
                new(72, 0, "The Sea of Clouds - Camp Cloudtop", new(-615.7473f, -118.36426f, 546.5934f)),
                new(73, 0, "The Sea of Clouds - Ok' Zundu", new(-613.1533f, -49.485046f, -415.03015f)),
            ],
            [612] =
            [
                new(98, 0, "Fringes - Castrum Oriens", new(-629.11426f, 132.89075f, -509.14783f)),
                new(99, 0, "Fringes - Peering Stones", new(415.3047f, 117.357056f, 246.75354f)),
            ],
            [613] =
            [
                new(105, 0, "Ruby Sea - Tamamizu", new(358.72437f, -118.05908f, -263.4165f)),
                new(106, 0, "Ruby Sea - Onokoro", new(88.181885f, 4.135132f, -583.3677f)),
            ],
            [614] =
            [
                new(107, 0, "Yanxia - Namai", new(432.66956f, 73.07532f, -90.74542f)),
                new(108, 0, "Yanxia - House of the Fierce", new(246.02112f, 9.079041f, -401.3581f)),
            ],
            [620] =
            [
                new(100, 0, "Peaks - Ala Gannha", new(114.579956f, 120.10376f, -747.06647f)),
                new(101, 0, "Peaks - Ala Ghiri", new(-271.3817f, 259.87634f, 748.86694f)),
            ],
            [621] =
            [
                new(102, 0, "Lochs - Porta Praetoria", new(-652.0333f, 53.391357f, -16.006714f)),
                new(103, 0, "Lochs - Ala Mhigan Quarter", new(612.4512f, 84.45862f, 656.82446f)),
            ],
            [622] =
            [
                new(109, 0, "Azim Steppe - Reunion", new(556.1454f, -16.800232f, 340.10828f)),
                new(110, 0, "Azim Steppe - Dawn Throne", new(78.26355f, 119.37134f, 36.301147f)),
                new(128, 0, "Azim Steppe - Dhoro Iloh", new(-754.63495f, 131.2428f, 116.5636f)),
            ],
            [813] =
            [
                new(132, 0, "Lakeland - Fort Jobb", new(753.7803f, 24.338135f, -28.82434f)),
                new(136, 0, "Lakeland - Ostall Imperative", new(-735.01184f, 53.391357f, -230.02979f)),
            ],
            [814] =
            [
                new(137, 0, "Kholusia - Stilltide", new(668.32983f, 29.465088f, 289.17358f)),
                new(138, 0, "Kholusia - Wright", new(-244.00702f, 20.736938f, 385.45813f)),
                new(139, 0, "Kholusia - Tomra", new(-426.38287f, 419.27222f, -623.5294f)),
            ],
            [815] =
            [
                new(140, 0, "Amh Araeng - Mord Souq", new(246.38745f, 12.985352f, -220.29456f)),
                new(141, 0, "Amh Araeng - Twine", new(-511.3451f, 47.989624f, -212.604f)),
                new(161, 0, "Amh Araeng - Inn at Journey's Head", new(399.0996f, -24.521301f, 307.97278f)),
            ],
            [816] =
            [
                new(144, 0, "Il Mheg - Lydha Lran", new(-344.71655f, 48.722046f, 512.2606f)),
                new(145, 0, "Il Mheg - Pla Enni", new(-72.55664f, 103.95972f, -857.35864f)),
                new(146, 0, "Il Mheg - Wolekdorf", new(380.51416f, 87.20532f, -687.2511f)),
            ],
            [817] =
            [
                new(142, 0, "Rak'tika - Slitherbough", new(-103.4104f, -19.333252f, 297.23047f)),
                new(143, 0, "Rak'tika - Fanow", new(382.77246f, 21.042175f, -194.11005f)),
            ],
            [818] =
            [
                new(147, 0, "Tempest - Ondo Cups", new(561.76074f, 352.62073f, -199.17603f)),
                new(148, 0, "Tempest - Macarenses Angle", new(-141.74109f, -280.5371f, 218.00562f)),
            ],
            [956] =
            [
                new(166, 0, "Labyrinthos - Archeion", new(443.5338f, 170.6416f, -476.18835f)),
                new(167, 0, "Labyrinthos - Sharlayan Hamlet", new(8.377136f, -27.542603f, -46.67737f)),
                new(168, 0, "Labyrinthos - Aporia", new(-729.18286f, -27.634155f, 302.1438f)),
            ],
            [957] =
            [
                new(169, 0, "Thavnair - Yedlihmad", new(193.49963f, 6.9733276f, 629.2362f)),
                new(170, 0, "Thavnair - Great Work", new(-527.48914f, 4.776001f, 36.75891f)),
                new(171, 0, "Thavnair - Palaka's Stand", new(405.1422f, 5.2643433f, -244.4953f)),
            ],
            [958] =
            [
                new(172, 0, "Garlemald - Camp Broken Glass", new(-408.10254f, 24.15503f, 479.9724f)),
                new(173, 0, "Garlemald - Tertium", new(518.9136f, -35.324707f, -178.36273f)),
            ],
            [959] =
            [
                new(174, 0, "Mare Lamentorum - Sinus Lacrimarum", new(-566.2471f, 134.66089f, 650.6294f)),
                new(175, 0, "Mare Lamentorum - Bestways Burrow", new(-0.015319824f, -128.83197f, -512.0165f)),
            ],
            [960] =
            [
                new(179, 0, "Ultima Thule - Reah Tahra", new(-544.152f, 74.32666f, 269.6421f)),
                new(180, 0, "Ultima Thule - Abode of the Ea", new(64.286255f, 272.48022f, -657.49603f)),
                new(181, 0, "Ultima Thule - Base Omicron", new(489.2804f, 437.5829f, 333.63843f)),
            ],
            [961] =
            [
                new(176, 0, "Elpis - Anagnorisis", new(159.96033f, 11.703674f, 126.878784f)),
                new(177, 0, "Elpis - Twelve Wonders", new(-633.7225f, -19.821533f, 542.56494f)),
                new(178, 0, "Elpis - Poieten Oikos", new(-529.9001f, 161.24207f, -222.2782f)),
            ],
            [1187] =
            [
                new(200, 0, "Urqopacha - Wachunpelo", new(332.96704f, -160.11298f, -416.22034f)),
                new(201, 0, "Urqopacha - Worlar's Echo", new(465.62903f, 114.94617f, 634.9126f)),
            ],
            [1188] =
            [
                new(202, 0, "Kozama'uka - Ok'hanu", new(-169.51251f, 6.576599f, -479.42322f)),
                new(203, 0, "Kozama'uka - Many Fires", new(541.16125f, 117.41809f, 203.60107f)),
                new(204, 0, "Kozama'uka - Earthenshire", new(-477.53113f, 124.04053f, 311.32983f)),
                new(238, 0, "Kozama'uka - Dock Poga", new(787.59436f, 14.175598f, -236.22491f)),
            ],
            [1189] =
            [
                new(205, 0, "Yak T'el - Iq Br'aax", new(-397.05505f, 23.5141f, -431.93713f)),
                new(206, 0, "Yak T'el - Mamook", new(721.40076f, -132.31104f, 526.1769f)),
            ],
            [1190] =
            [
                new(207, 0, "Shaaloani - Hhusatahwi", new(386.40417f, -0.19836426f, 467.61267f)),
                new(208, 0, "Shaaloani - Sheshenewezi Springs", new(-291.70673f, 19.08899f, -114.54956f)),
                new(209, 0, "Shaaloani - Mehwahhetsoan", new(311.36023f, -14.175659f, -567.74243f)),
            ],
            [1191] =
            [
                new(210, 0, "Heritage Found - Yyasulani Station", new(514.6105f, 145.86096f, 207.56836f)),
                new(211, 0, "Heritage Found - The Outskirts", new(-223.0412f, 31.937134f, -584.03906f)),
                new(212, 0, "Heritage Found - Electrope Strike", new(-219.53156f, 32.913696f, 120.77515f)),
            ],
            [1192] =
            [
                new(213, 0, "Living Memory - Leynode Mnemo", new(-0.22894287f, 57.175537f, 796.9634f)),
                new(214, 0, "Living Memory - Leynode Pyro", new(657.98413f, 28.976807f, -284.01617f)),
                new(215, 0, "Living Memory - Leynode Aero", new(-255.26825f, 59.433838f, -397.6654f)),
            ],
        };

    public static IReadOnlyList<TeleportDestination> Get(uint territoryId) =>
        ByTerritory.GetValueOrDefault(territoryId) ?? [];
}
