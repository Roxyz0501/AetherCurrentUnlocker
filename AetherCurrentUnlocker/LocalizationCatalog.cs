using System.Globalization;

namespace AetherCurrentUnlocker;

internal static class LocalizationCatalog
{
    private sealed record Entry(string De, string Fr, string Ko, string ZhHans, string ZhHant)
    {
        public string For(string code) => code switch
        {
            "de" => De, "fr" => Fr, "ko" => Ko, "zh-Hans" => ZhHans, "zh-Hant" => ZhHant, _ => string.Empty,
        };
    }

    private sealed record Full(string En, string Ja, string De, string Fr, string Ko, string ZhHans, string ZhHant)
    {
        public string For(string code) => code switch
        {
            "ja" => Ja, "de" => De, "fr" => Fr, "ko" => Ko, "zh-Hans" => ZhHans, "zh-Hant" => ZhHant, _ => En,
        };
    }

    private static readonly IReadOnlyDictionary<string, Entry> Static = BuildStatic();
    private static readonly IReadOnlyDictionary<string, Full> Formats = BuildFormats();

    public static string? Get(string english, string code) =>
        Static.TryGetValue(english, out Entry? entry) ? entry.For(code) : null;

    public static string GetRequired(string key, string code) =>
        Formats.TryGetValue(key, out Full? entry) ? entry.For(code) : key;

    public static void Verify()
    {
        foreach ((string key, Entry value) in Static)
            if (string.IsNullOrWhiteSpace(key) || new[] { value.De, value.Fr, value.Ko, value.ZhHans, value.ZhHant }.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException($"Incomplete static localization: {key}");

        foreach ((string key, Full value) in Formats)
        {
            string[] values = [value.En, value.Ja, value.De, value.Fr, value.Ko, value.ZhHans, value.ZhHant];
            if (values.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException($"Incomplete formatted localization: {key}");
            string signature = PlaceholderSignature(value.En);
            if (values.Skip(1).Any(x => PlaceholderSignature(x) != signature))
                throw new InvalidOperationException($"Placeholder mismatch: {key}");
        }
    }

    private static string PlaceholderSignature(string value) => string.Join(',',
        Enumerable.Range(0, 32).Where(i => value.Contains("{" + i, StringComparison.Ordinal)));

    private static IReadOnlyDictionary<string, Entry> BuildStatic()
    {
        Dictionary<string, Entry> d = new(StringComparer.Ordinal);
        void A(string en, string de, string fr, string ko, string zs, string zt) => d.Add(en, new(de, fr, ko, zs, zt));

        A("Idle", "Inaktiv", "Inactif", "대기 중", "空闲", "閒置");
        A("Stopped", "Angehalten", "Arrêté", "중지됨", "已停止", "已停止");
        A("Paused", "Pausiert", "En pause", "일시 정지", "已暂停", "已暫停");
        A("Selecting a target", "Ziel wird ausgewählt", "Sélection d'une cible", "대상 선택 중", "正在选择目标", "正在選擇目標");
        A("Selecting the next target", "Nächstes Ziel wird ausgewählt", "Sélection de la prochaine cible", "다음 대상 선택 중", "正在选择下一个目标", "正在選擇下一個目標");
        A("Waiting for player information", "Warten auf Spielerinformationen", "Attente des informations du joueur", "플레이어 정보 대기 중", "正在等待玩家信息", "正在等待玩家資訊");
        A("Waiting for area transition", "Warten auf Gebietswechsel", "Attente du changement de zone", "지역 전환 대기 중", "正在等待区域切换", "正在等待區域切換");
        A("Waiting until the character can be controlled", "Warten, bis der Charakter steuerbar ist", "Attente du contrôle du personnage", "캐릭터 조작 가능 상태 대기 중", "正在等待角色可操作", "正在等待角色可操作");
        A("Plugin unloaded", "Plugin entladen", "Plugin déchargé", "플러그인 종료됨", "插件已卸载", "外掛已卸載");
        A("Resuming target selection", "Zielauswahl wird fortgesetzt", "Reprise de la sélection", "대상 선택 재개 중", "正在继续选择目标", "正在繼續選擇目標");
        A("Resuming travel", "Reise wird fortgesetzt", "Reprise du déplacement", "이동 재개 중", "正在继续移动", "正在繼續移動");
        A("Finished processing the selected scope", "Ausgewählter Umfang abgeschlossen", "Traitement de la sélection terminé", "선택 범위 처리 완료", "所选范围处理完成", "所選範圍處理完成");
        A("Current location", "Aktueller Standort", "Position actuelle", "현재 위치", "当前位置", "目前位置");
        A("Already in the target area; traveling from the current location", "Bereits im Zielgebiet; Start vom aktuellen Standort", "Déjà dans la zone cible ; départ de la position actuelle", "대상 지역에 있어 현재 위치에서 이동합니다", "已在目标区域；从当前位置移动", "已在目標區域；從目前位置移動");
        A("Calculating a non-flying ground route", "Bodenroute ohne Flug wird berechnet", "Calcul d'un itinéraire au sol sans vol", "비행 없는 지상 경로 계산 중", "正在计算不飞行的地面路线", "正在計算不飛行的地面路線");
        A("ground detour", "Bodenumweg", "détour au sol", "지상 우회로", "地面绕行路线", "地面繞行路線");
        A("walking/ground-mount route", "Fuß-/Bodenreittierroute", "itinéraire à pied/monture terrestre", "도보/지상 탈것 경로", "步行/地面坐骑路线", "步行/地面坐騎路線");
        A("Waiting for the vnavmesh navigation mesh", "Warten auf das vnavmesh-Navigationsnetz", "Attente du maillage de navigation vnavmesh", "vnavmesh 내비게이션 메시 대기 중", "正在等待 vnavmesh 导航网格", "正在等待 vnavmesh 導航網格");
        A("Waiting for Teleport to become available", "Warten auf Teleport", "Attente de la disponibilité de Téléportation", "텔레포 사용 가능 상태 대기 중", "正在等待传送可用", "正在等待傳送可用");
        A("Waiting for area transition by Teleport", "Warten auf Gebietswechsel durch Teleport", "Attente du changement de zone par Téléportation", "텔레포 지역 전환 대기 중", "正在等待传送切换区域", "正在等待傳送切換區域");
        A("Waiting for Teleport cast/loading to finish (mount actions suppressed)", "Warten auf Teleport/Beladen (Reittieraktionen unterdrückt)", "Attente de la fin de Téléportation/du chargement (monture suspendue)", "텔레포/로딩 완료 대기 중 (탈것 동작 억제)", "正在等待传送/加载完成（暂停坐骑操作）", "正在等待傳送/載入完成（暫停坐騎操作）");
        A("Stopped because Teleport failed", "Wegen fehlgeschlagenem Teleport angehalten", "Arrêté car la Téléportation a échoué", "텔레포 실패로 중지됨", "因传送失败而停止", "因傳送失敗而停止");
        A("Stopped because Teleport did not complete", "Angehalten, da Teleport nicht abgeschlossen wurde", "Arrêté car la Téléportation ne s'est pas terminée", "텔레포 미완료로 중지됨", "因传送未完成而停止", "因傳送未完成而停止");
        A("Teleport started, but the target area was not reached within 45 seconds.", "Teleport gestartet, Zielgebiet aber nicht innerhalb von 45 Sekunden erreicht.", "Téléportation lancée, mais la zone cible n'a pas été atteinte en 45 secondes.", "텔레포를 시작했지만 45초 내에 대상 지역에 도착하지 못했습니다.", "传送已开始，但未在45秒内到达目标区域。", "傳送已開始，但未在45秒內到達目標區域。");
        A("Stopped because aetheryte coordinates could not be obtained", "Angehalten, da Ätheryten-Koordinaten fehlen", "Arrêté car les coordonnées de l'éthérite sont indisponibles", "에테라이트 좌표를 가져올 수 없어 중지됨", "因无法获取以太之光坐标而停止", "因無法取得以太之光座標而停止");
        A("Switching to a shorter aetheryte route instead of continuing to swim", "Wechsel zu einer kürzeren Ätherytenroute statt weiterzuschwimmen", "Passage à un trajet d'éthérite plus court au lieu de continuer à nager", "수영을 계속하는 대신 더 짧은 에테라이트 경로로 전환합니다", "切换到更短的以太之光路线，不再继续游泳", "切換到較短的以太之光路線，不再繼續游泳");
        A("Reached the detour waypoint; recalculating the route to the Aether Current", "Umwegpunkt erreicht; Route zur Windätherquelle wird neu berechnet", "Point de détour atteint ; recalcul de l'itinéraire vers le courant éthéré", "우회 지점 도착; 풍맥 경로 재계산 중", "已到达绕行点；正在重新计算风脉路线", "已到達繞行點；正在重新計算風脈路線");
        A("No reachable ground route found; trying another aetheryte candidate", "Keine erreichbare Bodenroute; anderer Ätheryt wird versucht", "Aucun itinéraire au sol accessible ; essai d'une autre éthérite", "도달 가능한 지상 경로가 없어 다른 에테라이트를 시도합니다", "未找到可达的地面路线；尝试其他以太之光", "未找到可達的地面路線；嘗試其他以太之光");
        A("No reachable ground route was found from any aetheryte candidate", "Von keinem Ätheryten wurde eine erreichbare Bodenroute gefunden", "Aucun itinéraire au sol accessible depuis les éthérites candidates", "어떤 에테라이트에서도 도달 가능한 지상 경로가 없습니다", "从任何候选以太之光都找不到可达地面路线", "從任何候選以太之光都找不到可達地面路線");
        A("Stopped after getting stuck on routes from every aetheryte", "Angehalten, nachdem alle Ätherytenrouten feststeckten", "Arrêté après blocage sur tous les trajets d'éthérite", "모든 에테라이트 경로에서 막혀 중지됨", "所有以太之光路线均卡住，已停止", "所有以太之光路線均卡住，已停止");
        A("Looking for the Aether Current object", "Windätherquelle wird gesucht", "Recherche du courant éthéré", "풍맥 오브젝트 확인 중", "正在寻找风脉对象", "正在尋找風脈物件");
        A("Waiting for the Aether Current object to load", "Warten auf die Windätherquelle", "Attente du chargement du courant éthéré", "풍맥 오브젝트 로딩 대기 중", "正在等待风脉对象加载", "正在等待風脈物件載入");
        A("Attuning to the Aether Current", "Einstimmung auf die Windätherquelle", "Harmonisation avec le courant éthéré", "풍맥 교감 중", "正在与风脉共鸣", "正在與風脈共鳴");
        A("Arrived, but the Aether Current object could not be found", "Angekommen, aber Windätherquelle nicht gefunden", "Arrivé, mais le courant éthéré est introuvable", "도착했지만 풍맥 오브젝트를 찾을 수 없습니다", "已到达，但未找到风脉对象", "已到達，但未找到風脈物件");
        A("Could not reach the target within five minutes", "Ziel nicht innerhalb von fünf Minuten erreicht", "Cible non atteinte en cinq minutes", "5분 내에 대상에 도달하지 못했습니다", "未能在五分钟内到达目标", "未能在五分鐘內到達目標");
        A("Mount Roulette", "Reittier-Roulette", "Monture aléatoire", "탈것 무작위 호출", "随机坐骑", "隨機坐騎");

        A("All expansions", "Alle Erweiterungen", "Toutes les extensions", "모든 확장팩", "所有资料片", "所有資料片");
        A("Current area", "Aktuelles Gebiet", "Zone actuelle", "현재 지역", "当前区域", "目前區域");
        A("Aether Current map", "Windätherquellen-Karte", "Carte des courants éthérés", "풍맥 지도", "风脉地图", "風脈地圖");
        A("Status", "Status", "État", "상태", "状态", "狀態");
        A("Support", "Unterstützen", "Soutien", "후원", "支持", "支援");
        A("Config", "Einstellungen", "Configuration", "설정", "设置", "設定");
        A("Language settings", "Sprache", "Langue", "언어 설정", "语言设置", "語言設定");
        A("Unlock current area  ▶", "Aktuelles Gebiet freischalten  ▶", "Débloquer la zone actuelle  ▶", "현재 지역 해금  ▶", "解锁当前区域  ▶", "解鎖目前區域  ▶");
        A("Unlock selected expansion  ▶", "Ausgewählte Erweiterung freischalten  ▶", "Débloquer l'extension choisie  ▶", "선택한 확장팩 모두 해금  ▶", "解锁所选资料片  ▶", "解鎖所選資料片  ▶");
        A("Select an expansion", "Erweiterung auswählen", "Choisir une extension", "확장팩 선택", "选择资料片", "選擇資料片");
        A("Stop", "Stopp", "Arrêter", "중지", "停止", "停止");
        A("Pause", "Pause", "Pause", "일시 정지", "暂停", "暫停");
        A("Resume", "Fortsetzen", "Reprendre", "재개", "继续", "繼續");
        A("Recalculate route", "Route neu berechnen", "Recalculer l'itinéraire", "경로 재계산", "重新计算路线", "重新計算路線");
        A("Display language", "Anzeigesprache", "Langue d'affichage", "표시 언어", "显示语言", "顯示語言");
        A("Targets", "Ziele", "Cibles", "처리 대상", "处理目标", "處理目標");
        A("Field Aether Currents", "Windätherquellen im Feld", "Courants éthérés sur le terrain", "필드 풍맥", "野外风脉", "野外風脈");
        A("Aether Current quests", "Windätherquellen-Aufträge", "Quêtes de courants éthérés", "풍맥 퀘스트", "风脉任务", "風脈任務");
        A("Confirm before running an expansion", "Vor Start einer Erweiterung bestätigen", "Confirmer avant de lancer une extension", "확장팩 실행 전 확인", "运行资料片前确认", "執行資料片前確認");
        A("Show debug information", "Debug-Informationen anzeigen", "Afficher les informations de débogage", "디버그 정보 표시", "显示调试信息", "顯示偵錯資訊");
        A("Commands", "Befehle", "Commandes", "명령어", "命令", "指令");
        A("Open the window", "Fenster öffnen", "Ouvrir la fenêtre", "창 열기", "打开窗口", "開啟視窗");
        A("Stop automation", "Automatisierung stoppen", "Arrêter l'automatisation", "자동화 중지", "停止自动化", "停止自動化");
        A("Debug", "Debug", "Débogage", "디버그", "调试", "偵錯");
        A("Travel mount", "Reisereittier", "Monture de voyage", "이동 탈것", "移动坐骑", "移動坐騎");
        A("Mount used for travel", "Reittier für Reisen", "Monture utilisée pour le trajet", "이동에 사용할 탈것", "移动时使用的坐骑", "移動時使用的坐騎");
        A("Plugin requirements", "Plugin-Anforderungen", "Prérequis des plugins", "플러그인 요구 사항", "插件要求", "外掛需求");
        A("Plugin", "Plugin", "Plugin", "플러그인", "插件", "外掛");
        A("Role", "Funktion", "Rôle", "용도", "用途", "用途");
        A("● Ready", "● Bereit", "● Prêt", "● 사용 가능", "● 就绪", "● 就緒");
        A("● Loaded", "● Geladen", "● Chargé", "● 로드됨", "● 已加载", "● 已載入");
        A("● Not loaded", "● Nicht geladen", "● Non chargé", "● 로드되지 않음", "● 未加载", "● 未載入");
        A("Shows whether each feature is available.", "Zeigt die Verfügbarkeit jeder Funktion.", "Indique si chaque fonction est disponible.", "각 기능의 사용 가능 여부를 표시합니다.", "显示各功能是否可用。", "顯示各功能是否可用。");
        A("Required and used directly for field travel and Questionable movement.", "Erforderlich; direkt für Feldreisen und Questionable-Bewegung verwendet.", "Requis et utilisé directement pour les trajets et les déplacements de Questionable.", "필드 이동 및 Questionable 이동에 직접 필요한 플러그인입니다.", "必需；直接用于野外移动和 Questionable 移动。", "必需；直接用於野外移動和 Questionable 移動。");
        A("Used directly for Aether Current quests; optional for field-current-only use.", "Direkt für Aufträge verwendet; bei reinen Feldquellen optional.", "Utilisé pour les quêtes ; facultatif pour les courants de terrain uniquement.", "풍맥 퀘스트에 사용되며 필드 풍맥만 처리할 때는 선택 사항입니다.", "直接用于风脉任务；仅处理野外风脉时可选。", "直接用於風脈任務；僅處理野外風脈時可選。");
        A("Questionable requirement (indirect dependency).", "Anforderung von Questionable (indirekte Abhängigkeit).", "Prérequis de Questionable (dépendance indirecte).", "Questionable 요구 사항(간접 종속성).", "Questionable 要求（间接依赖）。", "Questionable 需求（間接相依）。");
        A("Displayed area", "Angezeigtes Gebiet", "Zone affichée", "표시 지역", "显示区域", "顯示區域");
        A("Select an area", "Gebiet auswählen", "Choisir une zone", "지역 선택", "选择区域", "選擇區域");
        A("Select an area.", "Wähle ein Gebiet aus.", "Choisissez une zone.", "지역을 선택하세요.", "请选择区域。", "請選擇區域。");
        A("The map image for this area could not be loaded.", "Das Kartenbild dieses Gebiets konnte nicht geladen werden.", "L'image de la carte de cette zone n'a pas pu être chargée.", "이 지역의 지도 이미지를 불러올 수 없습니다.", "无法加载该区域的地图图像。", "無法載入該區域的地圖影像。");
        A("Map unavailable; showing a coordinate grid", "Karte nicht verfügbar; Koordinatengitter wird angezeigt", "Carte indisponible ; affichage d'une grille", "지도를 사용할 수 없어 좌표 격자를 표시합니다", "地图不可用；显示坐标网格", "地圖不可用；顯示座標網格");
        A("Unlocked", "Freigeschaltet", "Débloqué", "해금됨", "已解锁", "已解鎖");
        A("Locked", "Gesperrt", "Verrouillé", "미해금", "未解锁", "未解鎖");
        A("Current travel target", "Aktuelles Reiseziel", "Cible de trajet actuelle", "현재 이동 대상", "当前移动目标", "目前移動目標");
        A("Current teleport candidate", "Aktuelles Teleportziel", "Candidat de téléportation actuel", "현재 텔레포 후보", "当前传送候选", "目前傳送候選");
        A("● Unlocked", "● Freigeschaltet", "● Débloqué", "● 해금됨", "● 已解锁", "● 已解鎖");
        A("● Locked", "● Gesperrt", "● Verrouillé", "● 미해금", "● 未解锁", "● 未解鎖");
        A("● Current travel target", "● Aktuelles Reiseziel", "● Cible de trajet actuelle", "● 현재 이동 대상", "● 当前移动目标", "● 目前移動目標");
        A("● Aetheryte candidate", "● Ätherytenkandidat", "● Éthérite candidate", "● 에테라이트 후보", "● 以太之光候选", "● 以太之光候選");
        A("Field", "Feld", "Terrain", "필드", "野外", "野外");
        A("Quest", "Auftrag", "Quête", "퀘스트", "任务", "任務");
        A("Type", "Typ", "Type", "유형", "类型", "類型");
        A("Target", "Ziel", "Cible", "대상", "目标", "目標");
        A("Action", "Aktion", "Action", "작업", "操作", "操作");
        A("Field Aether Current", "Windätherquelle im Feld", "Courant éthéré sur le terrain", "필드 풍맥", "野外风脉", "野外風脈");
        A("Unlock this Aether Current", "Diese Windätherquelle freischalten", "Débloquer ce courant éthéré", "이 풍맥 해금", "解锁该风脉", "解鎖該風脈");
        A("Run this quest", "Diesen Auftrag ausführen", "Lancer cette quête", "이 퀘스트 진행", "运行该任务", "執行該任務");
        A("Available", "Verfügbar", "Disponible", "실행 가능", "可用", "可用");
        A("Locked (Questionable not detected)", "Gesperrt (Questionable nicht erkannt)", "Verrouillé (Questionable non détecté)", "미해금(Questionable 감지 안 됨)", "未解锁（未检测到 Questionable）", "未解鎖（未偵測到 Questionable）");
        A("Confirm expansion run", "Erweiterungslauf bestätigen", "Confirmer le lancement de l'extension", "확장팩 실행 확인", "确认运行资料片", "確認執行資料片");
        A("Start", "Start", "Démarrer", "시작", "开始", "開始");
        A("Cancel", "Abbrechen", "Annuler", "취소", "取消", "取消");
        A("Support Roxyz0501's development", "Roxyz0501s Entwicklung unterstützen", "Soutenir le développement de Roxyz0501", "Roxyz0501의 개발 후원", "支持 Roxyz0501 的开发", "支援 Roxyz0501 的開發");
        A("Support Roxyz0501 on Ko-fi", "Roxyz0501 auf Ko-fi unterstützen", "Soutenir Roxyz0501 sur Ko-fi", "Ko-fi에서 Roxyz0501 후원", "在 Ko-fi 上支持 Roxyz0501", "在 Ko-fi 上支援 Roxyz0501");
        A("Recipient: Roxyz0501", "Empfänger: Roxyz0501", "Bénéficiaire : Roxyz0501", "후원 대상: Roxyz0501", "收款人：Roxyz0501", "收款人：Roxyz0501");
        A("You may optionally support development of Aether Current Navigator. Every feature remains available without a contribution.", "Du kannst die Entwicklung von Aether Current Navigator freiwillig unterstützen. Alle Funktionen bleiben ohne Beitrag verfügbar.", "Vous pouvez soutenir facultativement le développement. Toutes les fonctions restent disponibles sans contribution.", "Aether Current Navigator 개발을 선택적으로 후원할 수 있습니다. 후원하지 않아도 모든 기능을 사용할 수 있습니다.", "你可以自愿支持 Aether Current Navigator 的开发。不捐助也可使用全部功能。", "你可以自願支援 Aether Current Navigator 的開發。不贊助也可使用全部功能。");
        A("Selected automatically from the game language on first launch, then kept until you change it.", "Beim ersten Start aus der Spielsprache gewählt und danach bis zur Änderung beibehalten.", "Choisie depuis la langue du jeu au premier lancement, puis conservée jusqu'à modification.", "첫 실행 시 게임 언어에서 선택되며 이후 변경할 때까지 유지됩니다.", "首次启动时根据游戏语言选择，之后保持不变直到手动更改。", "首次啟動時依遊戲語言選擇，之後保持不變直到手動變更。");
        A("logged-in character", "angemeldeter Charakter", "personnage connecté", "로그인한 캐릭터", "已登录角色", "已登入角色");
        A("The current area has no Aether Current data.", "Für das aktuelle Gebiet sind keine Windätherdaten vorhanden.", "La zone actuelle ne contient aucune donnée de courant éthéré.", "현재 지역에는 풍맥 데이터가 없습니다.", "当前区域没有风脉数据。", "目前區域沒有風脈資料。");
        A("No Aether Current data is available for the selected expansion.", "Für die ausgewählte Erweiterung sind keine Windätherdaten vorhanden.", "Aucune donnée n'est disponible pour l'extension choisie.", "선택한 확장팩에는 풍맥 데이터가 없습니다.", "所选资料片没有风脉数据。", "所選資料片沒有風脈資料。");
        A("The selected field Aether Current has no data.", "Keine Daten für die ausgewählte Feldquelle.", "Aucune donnée pour le courant de terrain choisi.", "선택한 필드 풍맥 데이터가 없습니다.", "所选野外风脉没有数据。", "所選野外風脈沒有資料。");
        A("The selected Aether Current quest has no data.", "Keine Daten für den ausgewählten Auftrag.", "Aucune donnée pour la quête choisie.", "선택한 풍맥 퀘스트 데이터가 없습니다.", "所选风脉任务没有数据。", "所選風脈任務沒有資料。");
        A("Automation is already running.", "Die Automatisierung läuft bereits.", "L'automatisation est déjà en cours.", "자동화가 이미 실행 중입니다.", "自动化已在运行。", "自動化已在執行。");
        A("Select field Aether Currents, Aether Current quests, or both.", "Wähle Feldquellen, Aufträge oder beides.", "Choisissez les courants de terrain, les quêtes ou les deux.", "필드 풍맥, 풍맥 퀘스트 또는 둘 다 선택하세요.", "请选择野外风脉、风脉任务或两者。", "請選擇野外風脈、風脈任務或兩者。");
        A("vnavmesh is required to travel to field Aether Currents.", "vnavmesh ist für die Reise zu Feldquellen erforderlich.", "vnavmesh est requis pour rejoindre les courants de terrain.", "필드 풍맥 이동에는 vnavmesh가 필요합니다.", "前往野外风脉需要 vnavmesh。", "前往野外風脈需要 vnavmesh。");
        A("vnavmesh is required to select a ground route to the quest issuer.", "vnavmesh ist für die Bodenroute zum Auftraggeber erforderlich.", "vnavmesh est requis pour l'itinéraire au sol vers le donneur de quête.", "퀘스트 발행자까지 지상 경로 선택에 vnavmesh가 필요합니다.", "选择前往任务发布者的地面路线需要 vnavmesh。", "選擇前往任務發布者的地面路線需要 vnavmesh。");
        A("Questionable is required to run Aether Current quests.", "Questionable ist für Windätheraufträge erforderlich.", "Questionable est requis pour exécuter les quêtes.", "풍맥 퀘스트 실행에는 Questionable이 필요합니다.", "运行风脉任务需要 Questionable。", "執行風脈任務需要 Questionable。");
        A("Questionable is already running. Stop it first.", "Questionable läuft bereits. Halte es zuerst an.", "Questionable est déjà actif. Arrêtez-le d'abord.", "Questionable이 이미 실행 중입니다. 먼저 중지하세요.", "Questionable 已在运行，请先停止。", "Questionable 已在執行，請先停止。");
        A("The requirements have not been met.", "Die Voraussetzungen sind nicht erfüllt.", "Les conditions ne sont pas remplies.", "요구 조건을 충족하지 못했습니다.", "未满足要求。", "未滿足需求。");
        A("Could not retrieve requirements from Questionable.", "Voraussetzungen konnten nicht von Questionable abgerufen werden.", "Impossible de récupérer les conditions depuis Questionable.", "Questionable에서 요구 조건을 가져올 수 없습니다.", "无法从 Questionable 获取要求。", "無法從 Questionable 取得需求。");
        A("Stopped by command.", "Durch Befehl angehalten.", "Arrêté par commande.", "명령으로 중지됨.", "已通过命令停止。", "已透過指令停止。");
        A("Stopped by user", "Vom Benutzer angehalten", "Arrêté par l'utilisateur", "사용자가 중지함", "用户已停止", "使用者已停止");
        A(" — possibly stuck; recalculate the route", " — möglicherweise festgefahren; Route neu berechnen", " — blocage possible ; recalculez l'itinéraire", " — 막혔을 수 있습니다. 경로를 재계산하세요", " — 可能已卡住；请重新计算路线", " — 可能已卡住；請重新計算路線");
        A(". Handing travel to Questionable", ". Reise wird an Questionable übergeben", ". Déplacement confié à Questionable", ". 이동을 Questionable에 넘깁니다", "。将移动交给 Questionable", "。將移動交給 Questionable");
        A(". Questionable refused to start it.", ". Questionable hat den Start abgelehnt.", ". Questionable a refusé de la lancer.", ". Questionable이 시작을 거부했습니다.", "。Questionable 拒绝启动。", "。Questionable 拒絕啟動。");
        A("Automation will stop because every aetheryte departure route got stuck.", "Die Automatisierung wird angehalten, da jede Ätherytenroute feststeckte.", "L'automatisation va s'arrêter car tous les trajets d'éthérite sont bloqués.", "모든 에테라이트 출발 경로에서 막혀 자동화를 중지합니다.", "所有以太之光出发路线均卡住，自动化将停止。", "所有以太之光出發路線均卡住，自動化將停止。");
        A("Available while traveling to a field Aether Current.", "Während der Reise zu einer Feldquelle verfügbar.", "Disponible pendant le trajet vers un courant de terrain.", "필드 풍맥으로 이동 중일 때 사용할 수 있습니다.", "前往野外风脉时可用。", "前往野外風脈時可用。");
        A("Cannot pause while teleporting or while Questionable is running", "Während Teleport oder Questionable nicht pausierbar", "Impossible de mettre en pause pendant une téléportation ou l'exécution de Questionable", "텔레포 또는 Questionable 실행 중에는 일시 정지할 수 없습니다", "传送或 Questionable 运行时无法暂停", "傳送或 Questionable 執行時無法暫停");
        A("Could not obtain normal aetheryte coordinates for this area.", "Normale Ätheryten-Koordinaten für dieses Gebiet konnten nicht abgerufen werden.", "Impossible d'obtenir les coordonnées normales des éthérites de cette zone.", "이 지역의 일반 에테라이트 좌표를 가져올 수 없습니다.", "无法获取该区域的普通以太之光坐标。", "無法取得該區域的普通以太之光座標。");
        A("Discard the current route and calculate a new ground route from your present location.", "Aktuelle Route verwerfen und vom jetzigen Standort neu berechnen.", "Abandonner l'itinéraire actuel et en calculer un nouveau depuis votre position.", "현재 경로를 버리고 현재 위치에서 새 지상 경로를 계산합니다.", "放弃当前路线并从当前位置计算新的地面路线。", "放棄目前路線並從目前位置計算新的地面路線。");
        A("Handing travel to Questionable because a ground route or NPC transport is required", "Reise wird an Questionable übergeben, da eine Bodenroute oder NPC-Beförderung nötig ist", "Déplacement confié à Questionable car un trajet au sol ou un transport PNJ est requis", "지상 경로 또는 NPC 이동이 필요하여 Questionable에 이동을 넘깁니다", "需要地面路线或 NPC 交通，移动交给 Questionable", "需要地面路線或 NPC 交通，移動交給 Questionable");
        A("Hover a number to view its Aether Current ID, unlock state, and in-game map coordinates.", "Fahre über eine Zahl, um ID, Status und Kartenkoordinaten anzuzeigen.", "Survolez un numéro pour afficher l'ID, l'état et les coordonnées en jeu.", "번호에 마우스를 올리면 풍맥 ID, 해금 상태, 게임 지도 좌표가 표시됩니다.", "将鼠标悬停在编号上可查看风脉ID、解锁状态和游戏地图坐标。", "將滑鼠停在編號上可查看風脈ID、解鎖狀態和遊戲地圖座標。");
        A("Opens Aether Current Navigator. Use /acnav stop to stop automation.", "Öffnet Aether Current Navigator. /acnav stop beendet die Automatisierung.", "Ouvre Aether Current Navigator. Utilisez /acnav stop pour arrêter l'automatisation.", "Aether Current Navigator를 엽니다. /acnav stop으로 자동화를 중지합니다.", "打开 Aether Current Navigator。使用 /acnav stop 停止自动化。", "開啟 Aether Current Navigator。使用 /acnav stop 停止自動化。");
        A("Stuck detected; switching to an unused aetheryte", "Feststecken erkannt; Wechsel zu einem ungenutzten Ätheryten", "Blocage détecté ; passage à une éthérite inutilisée", "막힘 감지; 사용하지 않은 에테라이트로 전환합니다", "检测到卡住；切换到未使用的以太之光", "偵測到卡住；切換到未使用的以太之光");
        A("Stuck detected. Automatically switching to an unused aetheryte.", "Feststecken erkannt. Automatischer Wechsel zu einem ungenutzten Ätheryten.", "Blocage détecté. Passage automatique à une éthérite inutilisée.", "막힘 감지. 사용하지 않은 에테라이트로 자동 전환합니다.", "检测到卡住，自动切换到未使用的以太之光。", "偵測到卡住，自動切換到未使用的以太之光。");
        A("The game map image was not found; using the fallback display.", "Spielkartenbild nicht gefunden; Ersatzanzeige wird verwendet.", "Image de carte introuvable ; utilisation de l'affichage de secours.", "게임 지도 이미지를 찾지 못해 대체 표시를 사용합니다.", "未找到游戏地图图像；使用备用显示。", "未找到遊戲地圖影像；使用備用顯示。");
        A("The target is unreachable without flying from the current location or any aetheryte candidate", "Das Ziel ist weder vom aktuellen Standort noch von einem Ätheryten ohne Flug erreichbar", "La cible est inaccessible sans vol depuis la position actuelle ou toute éthérite candidate", "현재 위치나 어떤 에테라이트에서도 비행 없이 대상에 도달할 수 없습니다", "从当前位置或任何候选以太之光都无法不飞行到达目标", "從目前位置或任何候選以太之光都無法不飛行到達目標");
        A("Not scanned", "Nicht gescannt", "Non analysé", "검사 안 됨", "未扫描", "未掃描");
        A("Unknown expansion ({0})", "Unbekannte Erweiterung ({0})", "Extension inconnue ({0})", "알 수 없는 확장팩 ({0})", "未知资料片（{0}）", "未知資料片（{0}）");
        return d;
    }

    private static IReadOnlyDictionary<string, Full> BuildFormats()
    {
        Dictionary<string, Full> d = new(StringComparer.Ordinal);
        void A(string key, string en, string ja, string de, string fr, string ko, string zs, string zt) => d.Add(key, new(en, ja, de, fr, ko, zs, zt));
        A("expansion.heavensward", "Heavensward", "蒼天のイシュガルド", "Heavensward", "Heavensward", "Heavensward", "苍穹之禁城", "蒼穹之禁城");
        A("expansion.stormblood", "Stormblood", "紅蓮のリベレーター", "Stormblood", "Stormblood", "Stormblood", "红莲之狂潮", "紅蓮之狂潮");
        A("expansion.shadowbringers", "Shadowbringers", "漆黒のヴィランズ", "Shadowbringers", "Shadowbringers", "Shadowbringers", "暗影之逆焰", "暗影之逆焰");
        A("expansion.endwalker", "Endwalker", "暁月のフィナーレ", "Endwalker", "Endwalker", "Endwalker", "晓月之终途", "曉月之終途");
        A("expansion.dawntrail", "Dawntrail", "黄金のレガシー", "Dawntrail", "Dawntrail", "Dawntrail", "黄金的遗产", "黃金的遺產");
        A("expansion.unknown", "Unknown expansion ({0})", "不明な拡張 ({0})", "Unbekannte Erweiterung ({0})", "Extension inconnue ({0})", "알 수 없는 확장팩 ({0})", "未知资料片（{0}）", "未知資料片（{0}）");
        A("dynamic.quest", "Quest {0}", "クエスト {0}", "Auftrag {0}", "Quête {0}", "퀘스트 {0}", "任务 {0}", "任務 {0}");
        A("dynamic.aetheryte", "Aetheryte {0}", "エーテライト {0}", "Ätheryt {0}", "Éthérite {0}", "에테라이트 {0}", "以太之光 {0}", "以太之光 {0}");
        A("ui.processing", "Processing: {0}  ({1}/{2})", "処理中: {0}  ({1}/{2})", "Verarbeitung: {0}  ({1}/{2})", "Traitement : {0}  ({1}/{2})", "처리 중: {0}  ({1}/{2})", "处理中：{0}  ({1}/{2})", "處理中：{0}  ({1}/{2})");
        A("ui.routeCandidate", "Route candidate: {0}/{1}  {2}  horizontal distance {3}", "経路候補: {0}/{1}  {2}  水平距離 {3}", "Routenkandidat: {0}/{1}  {2}  horizontale Entfernung {3}", "Itinéraire candidat : {0}/{1}  {2}  distance horizontale {3}", "경로 후보: {0}/{1}  {2}  수평 거리 {3}", "路线候选：{0}/{1}  {2}  水平距离 {3}", "路線候選：{0}/{1}  {2}  水平距離 {3}");
        A("ui.latestNotice", "Latest notice: {0}", "直近の注意: {0}", "Letzter Hinweis: {0}", "Dernier avis : {0}", "최근 알림: {0}", "最新提示：{0}", "最新提示：{0}");
        A("ui.recalculations", "Route recalculations for this current: {0}", "この風脈での通路再計算: {0} 回", "Routenneuberechnungen für diese Quelle: {0}", "Recalculs pour ce courant : {0}", "이 풍맥의 경로 재계산: {0}회", "该风脉的路线重新计算次数：{0}", "該風脈的路線重新計算次數：{0}");
        A("ui.characterSettings", "Per-character settings — {0}", "キャラクター別設定 — {0}", "Charakterbezogene Einstellungen — {0}", "Paramètres par personnage — {0}", "캐릭터별 설정 — {0}", "角色设置 — {0}", "角色設定 — {0}");
        A("ui.currentTooltip", "#{0} Aether Current {1}", "#{0} 風脈 {1}", "#{0} Windätherquelle {1}", "#{0} Courant éthéré {1}", "#{0} 풍맥 {1}", "#{0} 风脉 {1}", "#{0} 風脈 {1}");
        A("ui.mapCoordinates", "Map coordinates X:{0:F1} Y:{1:F1}", "マップ座標 X:{0:F1} Y:{1:F1}", "Kartenkoordinaten X:{0:F1} Y:{1:F1}", "Coordonnées de carte X:{0:F1} Y:{1:F1}", "지도 좌표 X:{0:F1} Y:{1:F1}", "地图坐标 X:{0:F1} Y:{1:F1}", "地圖座標 X:{0:F1} Y:{1:F1}");
        A("ui.worldCoordinates", "World coordinates X:{0:F1} Y:{1:F1} Z:{2:F1}", "ワールド座標 X:{0:F1} Y:{1:F1} Z:{2:F1}", "Weltkoordinaten X:{0:F1} Y:{1:F1} Z:{2:F1}", "Coordonnées du monde X:{0:F1} Y:{1:F1} Z:{2:F1}", "월드 좌표 X:{0:F1} Y:{1:F1} Z:{2:F1}", "世界坐标 X:{0:F1} Y:{1:F1} Z:{2:F1}", "世界座標 X:{0:F1} Y:{1:F1} Z:{2:F1}");
        A("ui.aetheryteCandidate", "Aetheryte candidate: {0}", "エーテライト候補: {0}", "Ätherytenkandidat: {0}", "Éthérite candidate : {0}", "에테라이트 후보: {0}", "以太之光候选：{0}", "以太之光候選：{0}");
        A("ui.aetheryteDiagnostics", "Aetheryte diagnostics: {0}", "エーテライト取得診断: {0}", "Ätheryten-Diagnose: {0}", "Diagnostic des éthérites : {0}", "에테라이트 진단: {0}", "以太之光诊断：{0}", "以太之光診斷：{0}");
        A("ui.confirmExpansion", "Process locked Aether Currents in “{0}” in sequence. Normal teleport fees apply, and Questionable runs each Aether Current quest individually.", "「{0}」の未解放風脈を順番に処理します。通常テレポ代が発生し、風脈クエストではQuestionableが各クエストを単独実行します。", "Gesperrte Windätherquellen in „{0}“ werden nacheinander verarbeitet. Normale Teleportgebühren fallen an; Questionable führt jeden Auftrag einzeln aus.", "Traite les courants verrouillés de « {0} » dans l'ordre. Les frais normaux s'appliquent et Questionable exécute chaque quête séparément.", "'{0}'의 미해금 풍맥을 순서대로 처리합니다. 일반 텔레포 비용이 들며 Questionable이 각 퀘스트를 개별 실행합니다.", "依次处理“{0}”中未解锁的风脉。会产生普通传送费用，Questionable 将逐个运行风脉任务。", "依序處理「{0}」中未解鎖的風脈。會產生一般傳送費用，Questionable 將逐一執行風脈任務。");
        A("auto.unavailableSuffix", " ({0} unavailable; review the details)", "（実行不能 {0} 件。詳細を確認してください）", " ({0} nicht verfügbar; Details prüfen)", " ({0} indisponible(s) ; consultez les détails)", " ({0}개 사용 불가; 세부 정보 확인)", "（{0}项不可用；请查看详情）", "（{0}項不可用；請查看詳情）");
        A("auto.checkingArea", "Area {0}/{1}: checking Aether Currents in {2}", "エリア {0}/{1}: {2} の風脈を確認中", "Gebiet {0}/{1}: Windätherquellen in {2} werden geprüft", "Zone {0}/{1} : vérification des courants de {2}", "지역 {0}/{1}: {2}의 풍맥 확인 중", "区域 {0}/{1}：正在检查 {2} 的风脉", "區域 {0}/{1}：正在檢查 {2} 的風脈");
        A("auto.prepareCurrent", "Nearest target selected: preparing field Aether Current {0}", "近い対象を選択: フィールド風脈 {0} を準備中", "Nächstes Ziel: Feldquelle {0} wird vorbereitet", "Cible la plus proche : préparation du courant de terrain {0}", "가장 가까운 대상 선택: 필드 풍맥 {0} 준비 중", "已选择最近目标：正在准备野外风脉 {0}", "已選擇最近目標：正在準備野外風脈 {0}");
        A("auto.prepareQuest", "Nearest target selected: preparing travel to the issuer of “{0}”", "近い対象を選択: 「{0}」の受注地点へ移動準備中", "Nächstes Ziel: Reise zum Auftraggeber von „{0}“ wird vorbereitet", "Cible la plus proche : préparation du trajet vers le donneur de « {0} »", "가장 가까운 대상 선택: '{0}' 수주 위치로 이동 준비 중", "已选择最近目标：正在准备前往“{0}”的接取地点", "已選擇最近目標：正在準備前往「{0}」的接取地點");
        A("auto.questRefused", "Questionable refused to start “{0}”.", "Questionableが「{0}」の開始を拒否しました。", "Questionable hat den Start von „{0}“ abgelehnt.", "Questionable a refusé de lancer « {0} ».", "Questionable이 '{0}' 시작을 거부했습니다.", "Questionable 拒绝启动“{0}”。", "Questionable 拒絕啟動「{0}」。");
        A("auto.areaFinished", "Finished {0}. Moving to the next area ({1}/{2})", "{0} の処理が完了しました。次のエリアへ進みます（{1}/{2}）", "{0} abgeschlossen. Wechsel zum nächsten Gebiet ({1}/{2})", "{0} terminée. Passage à la zone suivante ({1}/{2})", "{0} 처리 완료. 다음 지역으로 이동 ({1}/{2})", "{0} 已完成。前往下一个区域（{1}/{2}）", "{0} 已完成。前往下一個區域（{1}/{2}）");
        A("auto.runningQuest", "Running “{0}” with Questionable", "Questionableで「{0}」を進行中", "„{0}“ wird mit Questionable ausgeführt", "Exécution de « {0} » avec Questionable", "Questionable로 '{0}' 진행 중", "正在使用 Questionable 运行“{0}”", "正在使用 Questionable 執行「{0}」");
        A("auto.questStopped", "“{0}” stopped before completion.", "「{0}」は完了前に停止しました。", "„{0}“ wurde vor Abschluss angehalten.", "« {0} » s'est arrêtée avant la fin.", "'{0}'이 완료 전에 중지되었습니다.", "“{0}”在完成前停止。", "「{0}」在完成前停止。");
        A("auto.comparingRoutes", "Comparing ground-route distances from the current location and all {0} aetheryte candidates", "現在地点と全エーテライト候補の地上経路距離を比較中（全{0}候補）", "Bodenrouten vom aktuellen Standort und allen {0} Ätheryten werden verglichen", "Comparaison des trajets au sol depuis la position actuelle et les {0} éthérites", "현재 위치와 에테라이트 후보 {0}개의 지상 경로 거리 비교 중", "正在比较当前位置与全部 {0} 个以太之光候选的地面路线距离", "正在比較目前位置與全部 {0} 個以太之光候選的地面路線距離");
        A("auto.currentGroundRoute", "Using the ground route from the current location ({0:F0}m, no flying)", "現在地点からの地上経路を採用（{0:F0}m、飛行なし）", "Bodenroute vom aktuellen Standort ({0:F0}m, ohne Flug)", "Itinéraire au sol depuis la position actuelle ({0:F0}m, sans vol)", "현재 위치의 지상 경로 사용 ({0:F0}m, 비행 없음)", "使用当前位置的地面路线（{0:F0}m，不飞行）", "使用目前位置的地面路線（{0:F0}m，不飛行）");
        A("auto.aetheryteGroundRoute", "Using a reachable ground route from “{0}” ({1:F0}m, candidate {2}/{3})", "到達可能な「{0}」からの地上経路を採用（{1:F0}m、候補 {2}/{3}）", "Erreichbare Bodenroute ab „{0}“ ({1:F0}m, Kandidat {2}/{3})", "Itinéraire au sol accessible depuis « {0} » ({1:F0}m, candidat {2}/{3})", "'{0}'에서 도달 가능한 지상 경로 사용 ({1:F0}m, 후보 {2}/{3})", "使用从“{0}”出发的可达地面路线（{1:F0}m，候选 {2}/{3}）", "使用從「{0}」出發的可達地面路線（{1:F0}m，候選 {2}/{3}）");
        A("auto.noAetheryteCoordinates", "Could not obtain normal aetheryte coordinates for territory {0}.", "対象エリア {0} の通常エーテライト座標を取得できませんでした。", "Normale Ätheryten-Koordinaten für Gebiet {0} konnten nicht abgerufen werden.", "Impossible d'obtenir les coordonnées des éthérites de la zone {0}.", "지역 {0}의 일반 에테라이트 좌표를 가져올 수 없습니다.", "无法获取区域 {0} 的普通以太之光坐标。", "無法取得區域 {0} 的普通以太之光座標。");
        A("auto.fromNearestAetheryte", "Traveling from the nearest aetheryte, “{0}”", "最寄りの「{0}」付近から向かいます", "Reise vom nächsten Ätheryten „{0}“", "Départ depuis l'éthérite la plus proche, « {0} »", "가장 가까운 에테라이트 '{0}'에서 이동합니다", "从最近的以太之光“{0}”出发", "從最近的以太之光「{0}」出發");
        A("auto.teleportingCandidate", "Candidate {0}/{1}: teleporting to “{2}”", "候補 {0}/{1}: 「{2}」へ通常テレポ中", "Kandidat {0}/{1}: Teleport zu „{2}“", "Candidat {0}/{1} : téléportation vers « {2} »", "후보 {0}/{1}: '{2}'로 텔레포 중", "候选 {0}/{1}：正在传送至“{2}”", "候選 {0}/{1}：正在傳送至「{2}」");
        A("auto.teleportFailed", "Failed to start Teleport to “{0}”.", "「{0}」への通常テレポ開始に失敗しました。", "Teleport zu „{0}“ konnte nicht gestartet werden.", "Impossible de lancer la téléportation vers « {0} ».", "'{0}'로 텔레포를 시작하지 못했습니다.", "无法开始传送至“{0}”。", "無法開始傳送至「{0}」。");
        A("auto.teleportArrivalTimeout", "Teleport to “{0}” started, but arrival could not be confirmed by coordinates within 45 seconds.", "「{0}」への通常テレポを開始しましたが、45秒以内に到着を座標確認できませんでした。", "Teleport zu „{0}“ gestartet, aber Ankunft nicht innerhalb von 45 Sekunden bestätigt.", "Téléportation vers « {0} » lancée, mais l'arrivée n'a pas été confirmée en 45 secondes.", "'{0}'로 텔레포를 시작했지만 45초 내에 좌표로 도착을 확인하지 못했습니다.", "已开始传送至“{0}”，但未能在45秒内通过坐标确认到达。", "已開始傳送至「{0}」，但未能在45秒內透過座標確認到達。");
        A("auto.waitingArrival", "Waiting to confirm Teleport arrival ({0:F0}m from aetheryte)", "通常テレポの到着確認待ち（エーテライトまで {0:F0}m）", "Warten auf Teleportankunft ({0:F0}m vom Ätheryten)", "Attente de confirmation d'arrivée ({0:F0}m de l'éthérite)", "텔레포 도착 확인 대기 중 (에테라이트에서 {0:F0}m)", "正在等待确认传送到达（距以太之光 {0:F0}m）", "正在等待確認傳送到達（距以太之光 {0:F0}m）");
        A("auto.allRoutesStuck", "{0}: stuck on routes from every aetheryte in the map.", "{0}: マップ内のすべてのエーテライト出発経路でスタックしました。", "{0}: auf Routen von jedem Ätheryten festgefahren.", "{0} : blocage sur les trajets de toutes les éthérites.", "{0}: 지도의 모든 에테라이트 출발 경로에서 막혔습니다.", "{0}：从地图中每个以太之光出发的路线都卡住了。", "{0}：從地圖中每個以太之光出發的路線都卡住了。");
        A("auto.verifiedGroundRoute", "Following a verified ground route to the Aether Current ({0:F0}m remaining, no flying)", "風脈まで検証済み地上経路で移動中（残り {0:F0}m、飛行なし）", "Verifizierte Bodenroute zur Quelle ({0:F0}m verbleibend, ohne Flug)", "Itinéraire au sol vérifié ({0:F0}m restants, sans vol)", "검증된 지상 경로로 풍맥 이동 중 ({0:F0}m 남음, 비행 없음)", "沿验证过的地面路线前往风脉（剩余 {0:F0}m，不飞行）", "沿驗證過的地面路線前往風脈（剩餘 {0:F0}m，不飛行）");
        A("auto.followingRoute", "Following the {0} to the Aether Current ({1:F0}m remaining, no flying)", "風脈まで{0}で移動中（残り {1:F0}m、飛行なし）", "Auf {0} zur Quelle ({1:F0}m verbleibend, ohne Flug)", "Trajet via {0} ({1:F0}m restants, sans vol)", "{0}로 풍맥 이동 중 ({1:F0}m 남음, 비행 없음)", "沿{0}前往风脉（剩余 {1:F0}m，不飞行）", "沿{0}前往風脈（剩餘 {1:F0}m，不飛行）");
        A("auto.normalTravel", "Traveling normally to the Aether Current ({0:F0}m remaining){1}", "風脈まで正規移動中（残り {0:F0}m）{1}", "Normale Reise zur Quelle ({0:F0}m verbleibend){1}", "Déplacement normal vers le courant ({0:F0}m restants){1}", "일반 이동으로 풍맥 이동 중 ({0:F0}m 남음){1}", "正常前往风脉（剩余 {0:F0}m）{1}", "正常前往風脈（剩餘 {0:F0}m）{1}");
        A("auto.currentIssue", "Aether Current {0} (territory {1}): {2}", "風脈 {0}（エリア {1}）: {2}", "Windätherquelle {0} (Gebiet {1}): {2}", "Courant éthéré {0} (zone {1}) : {2}", "풍맥 {0} (지역 {1}): {2}", "风脉 {0}（区域 {1}）：{2}", "風脈 {0}（區域 {1}）：{2}");
        A("auto.questIssue", "“{0}”: {1}", "「{0}」: {1}", "„{0}“: {1}", "« {0} » : {1}", "'{0}': {1}", "“{0}”：{1}", "「{0}」：{1}");
        A("auto.recalculating", "Recalculating the route from the current location (attempt {0})", "現在地点から通路を再計算します（{0}回目）", "Route vom aktuellen Standort wird neu berechnet (Versuch {0})", "Recalcul depuis la position actuelle (tentative {0})", "현재 위치에서 경로 재계산 중 ({0}번째 시도)", "正在从当前位置重新计算路线（第 {0} 次）", "正在從目前位置重新計算路線（第 {0} 次）");
        A("auto.retrying", "{0}: retrying from “{1}” ({2}/{3})", "{0}: 「{1}」から再試行（{2}/{3}）", "{0}: erneuter Versuch ab „{1}“ ({2}/{3})", "{0} : nouvel essai depuis « {1} » ({2}/{3})", "{0}: '{1}'에서 재시도 ({2}/{3})", "{0}：从“{1}”重试（{2}/{3}）", "{0}：從「{1}」重試（{2}/{3}）");
        A("auto.selectedMount", "selected mount (ID {0})", "指定マウント（ID {0}）", "ausgewähltes Reittier (ID {0})", "monture choisie (ID {0})", "선택한 탈것 (ID {0})", "所选坐骑（ID {0}）", "所選坐騎（ID {0}）");
        A("data.aetheryteScan", "Territory {0}: Dalamud list {1} / territory matches {2} / bundled coordinates {3} / normal Excel entries {4} / Excel coordinates {5}", "Territory {0}: Dalamud一覧 {1}件 / エリア一致 {2}件 / 同梱座標 {3}件 / Excel通常 {4}件 / Excel座標 {5}件", "Gebiet {0}: Dalamud-Liste {1} / Gebietstreffer {2} / gebündelte Koordinaten {3} / normale Excel-Einträge {4} / Excel-Koordinaten {5}", "Zone {0} : liste Dalamud {1} / correspondances {2} / coordonnées incluses {3} / entrées Excel normales {4} / coordonnées Excel {5}", "지역 {0}: Dalamud 목록 {1} / 지역 일치 {2} / 포함 좌표 {3} / 일반 Excel 항목 {4} / Excel 좌표 {5}", "区域 {0}：Dalamud 列表 {1} / 区域匹配 {2} / 内置坐标 {3} / 普通 Excel 条目 {4} / Excel 坐标 {5}", "區域 {0}：Dalamud 清單 {1} / 區域符合 {2} / 內建座標 {3} / 一般 Excel 項目 {4} / Excel 座標 {5}");
        return d;
    }
}
