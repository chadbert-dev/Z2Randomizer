using DynamicData;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Z2Randomizer.RandomizerCore.Overworld;
using Z2Randomizer.RandomizerCore.Sidescroll.Town;

namespace Z2Randomizer.RandomizerCore;

// Gradually add indexes here as needed. I think in the end we will probably have two Text lists as well.
public enum DialogWest
{
    ALREADY_HAVE_ITEM = 16,
}

// In the CustomTexts constants, the east indexes are +52. This enum however, uses the indexes the assembly uses.
public enum DialogEast
{
    NOT_ENOUGH_CONTAINERS = 17,
    ALREADY_HAVE_ITEM = 19,                // not used if spells are in the pool
    OLD_KASUTO_NOT_ENOUGH_CONTAINERS = 43, // not used if spells are in the pool
}

public class CustomTexts
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private static int TOWNSFOLK_TEXT_ATTEMPT_LIMIT = 100;
    private static readonly Text ERROR_TEXT = new("ERROR$GENERATING$TEXT");

    //Experimental. If people like this it may get a flag or just be always on
    private const bool OLD_KASUTO_HINT_IS_ALWAYS_GOOD = true;
    private const bool HELPFUL_HINTS_TREAT_BAGU_AS_TOWN = false;

    private const int errorTextIndex1 = 25; // Error inside house on 1st screen in Ruto, 1st message
    private const int errorTextIndex2 = 26; // Error 2nd message

    private const int SariaGreeterNpc = 28;

    private const int talkingBotIndexSleeping = 49; // Sleeping Bot in Saria inside house on 2nd screen, Zzz... message the first 3 talks
    private const int talkingBotIndexTalking = 50; // Sleeping Bot final message

    private const int talkingAcheIndexSleeping = 85; // Ache in Nabooru inside house on 3rd screen, ***** message the first 3 talks
    private const int talkingAcheIndexTalking = 97; // Ache final message

    private static readonly int[] rauruHints = [32 /* lady outside on 1st screen */, 30 /* man inside house on 1st screen */, 12 /* kid inside house on 2nd screen */];
    private static readonly int[] rutoHints = [errorTextIndex1, 18 /* lady outside on 2nd screen */, 33 /* woman inside on 2nd screen */];
    private static readonly int[] sariaHints = [28 /* greeting man at town entrance */, talkingBotIndexTalking];
    private static readonly int[] midoHints = [45 /* man inside house on 1st screen */];
    private static readonly int[] kingsTomb = [51];

    private static readonly int[] nabooruHints = [67 /* man inside house on 1st screen */, 64 /* lady outside on 3rd screen */, talkingAcheIndexTalking];
    private static readonly int[] daruniaHints = [77 /* text on wall inside house on 1st screen */, 73 /* kid outside on 3rd screen */];
    private static readonly int[] newkasutoHints = [83 /* greeting lady at town entrance */, 68 /* text on wall inside house on 1st screen */, /*92 unreachable Lady in Magic Container house (removed) */];
    private static readonly int[] oldkasutoHint = [74 /* readable wall inside last house on 2nd screen */];

    private static readonly int[] genericWestMoving = [4, 5, 6, 7, 8];
    private static readonly int[] genericEastMoving = [56, 57, 58, 59];
    private static readonly int[] rauruMoving = [9, 10];
    private static readonly int[] rutoMoving = [17, 19];
    private static readonly int[] sariaMoving = [27];
    private static readonly int[] movingMido = [40, 39];
    private static readonly int[] movingNabooru = [61, 60];
    private static readonly int[] daruniaMoving = [72, 75];
    private static readonly int[] newkasutoMoving = [88, 89];

    private static readonly List<int> kidHintNpcs = [12, 73, 77];
    private static readonly List<int> outsideHintNpcs = [18, 28, 32, 64, 73, 83];


    private static readonly Collectable[] smallItems = [
        Collectable.BLUE_JAR,
        Collectable.XL_BAG,
        Collectable.KEY,
        Collectable.MEDIUM_BAG,
        Collectable.MAGIC_CONTAINER,
        Collectable.HEART_CONTAINER,
        Collectable.ONEUP,
        Collectable.RED_JAR,
        Collectable.SMALL_BAG,
        Collectable.LARGE_BAG
    ];

    //Indexes in the hints list
    private static readonly Dictionary<TownType, int> TOWN_SIGN_INDEXES = new()
    {
        {TownType.RAURU, 11},
        {TownType.RUTO, 20},
        {TownType.SARIA, 29},
        {TownType.MIDO, 41},
        {TownType.NABOORU, 62},
        {TownType.DARUNIA, 76},
        {TownType.NEW_KASUTO, 86},
        {TownType.OLD_KASUTO, 94},
    };

    public const int WEST_TEXT_COUNT = 52;
    private const int MAX_TEXT_LENGTH = 3134;
    private const int MAX_TEXT_ATTEMPTS = 1000;
    private const int numberOfTextEntries = 98;

    // On the off chance we do something that eats up space in PRG3, we can limit
    // the silly dialog here so that it doesn't eat up extra space for no reason.
    private const int MOVING_NPC_DIALOG_MAX_BYTES = 3000;
    //TODO: This should just be a listing of every text index by continent, but for now it's a pile.
    private const int westAlreadyGotItemTextIndex = 16;
    private const int eastAlreadyGotItemTextIndex = 71;
    private const int bridgeTextIndex = 37;
    private const int riverManTextIndex = 36;
    private const int downstabClosedDoorTextIndex = 42;
    private const int downstabGuyGotItemTextIndex = 47;
    private const int upstabClosedDoorTextIndex = 78;
    private const int upstabGuyGotItemTextIndex = 82;
    private const int trophySpellHintIndex = 13;
    private const int medicineSpellHintIndex = 43;
    private const int childSpellHintIndex = 79;
    private const int waterSpellHintIndex = 65;
    private const int mirrorSpellHintIndex = 22;

    private const int baguTextIndex = 48;
    private const int gotWaterTextIndex = 63;
    private const int gotMirrorTextIndex = 21;

    private const int HELPFUL_HINTS_COUNT = 4;

    //private static readonly Dictionary<Town, int> spellTextIndexes = { 15, 24, 35, 46, 70, 81, 93, 96 };
    private static readonly Dictionary<int, int> townsfolkTextIndexes = new()
    {
        { (int)VanillaTownMap.RAURU_WIZARD, 15 },
        { (int)VanillaTownMap.RUTO_WIZARD, 24 },
        { (int)VanillaTownMap.SARIA_WIZARD, 35 },
        { (int)VanillaTownMap.MIDO_WIZARD, 46 },
        { (int)VanillaTownMap.NABOORU_WIZARD, 70 },
        { (int)VanillaTownMap.DARUNIA_WIZARD, 81 },
        { (int)VanillaTownMap.NEW_KASUTO_WIZARD, 93 },
        { (int)VanillaTownMap.OLD_KASUTO_WIZARD, 96 },
        { (int)VanillaTownMap.BAGU_INDOORS, baguTextIndex },
        { (int)VanillaTownMap.MIDO_TRAINER, downstabGuyGotItemTextIndex },
        { (int)VanillaTownMap.DARUNIA_TRAINER, upstabGuyGotItemTextIndex },
        { (int)VanillaTownMap.SARIA_TABLE, gotMirrorTextIndex },
        { (int)VanillaTownMap.NABOORU_MID, gotWaterTextIndex },
    };

    private static readonly Dictionary<Collectable, int> wizardTextIndexesBySpell = new()
    {
        { Collectable.SHIELD_SPELL, 15 },
        { Collectable.JUMP_SPELL, 24 },
        { Collectable.TANOOKI, 24 },
        { Collectable.LIFE_SPELL, 35 },
        { Collectable.FAIRY_SPELL, 46 },
        { Collectable.FIRE_SPELL, 70 },
        { Collectable.DASH_SPELL, 70 },
        { Collectable.REFLECT_SPELL, 81 },
        { Collectable.SPELL_SPELL, 93 },
        { Collectable.THUNDER_SPELL, 96 },
    };

    private static readonly int[][] hintIndexes = [rauruHints, rutoHints, sariaHints, midoHints, 
        nabooruHints, daruniaHints, newkasutoHints, oldkasutoHint, kingsTomb];

    public static readonly string[] GENERIC_WIZARD_TEXTS =
    [
        "do you know$why we$stopped$the car?",
        "link...$i am your$father",
        "I like big$bots and i$cannot lie",
        "why am i$locked in$a basement",
        "thats just$like your$opinion$man",
        "the dude$abides",
        "boy this$is really$expensive",
        "10th enemy$has the$bomb",
        "stay$awhile and$listen",
        "you teach$me a spell",
        "you know$nothing",
        "thats what$she said",
        "jump in$lava for$200 rupees",
        "you wont$be able$to cast$this",
        "big bucks$no whammys",
        "bagu owes$me 20$rupees",
        "you are$the$weakest$link",
        "link i$am your$father",
        "theres no$wifi here",
        "a wild$link$appears",
        "whats the$wifi$password",
        "dont send$me back to$the home",
        "id like$to buy a$vowel",
        "i only$know one$spell",
        "i went$to$college$for this",
        "this game$needs more$categories",
        "who$picked$these$flags",
        "i found$this in$the$garbage",
        "have you$heard my$mixtape",
        "Does this$robe make$me look$fat?",
        "No pom pom$shaking$here",
        "Youre$a wizard,$Link",
        "Take any$Robe you$want",
        "Dont Move$I dropped$a contact$lens",
        "Please$support$ZSR",
        "This wont$hurt a bit",
        "How many$shakes can$a digshake$shake?",
        "Free$your mind",
        "da$na na na$naaaaaaaaa",
        "Join the$Nintendo$Power$Club",
        "Silvers$are in$palace 1",
        "Needs$more$Cowbell",
        "Which$timeline$is this?",
        "Hurry!$I have to$preheat$the oven",
        "Poyo!",
        "Sploosh$Kaboom!",
        "Let me$read my$Vogon$Poetry",
        "somebody$set up us$the bomb",
        "boat$league$confirmed"
    ];

    public static readonly string[] RIVER_MAN_TEXTS =
    [
        "bagu said$what? that$jerk!",
        "try not$to drown",
        "who is$bagu? i$dont know$any bagu",
        "3 5 10 7$12 4 11 6$1 13 14 2$15 8 9",
        "why cant$you swim?",
        "what is$your$quest?",
        "tickets$please",
        "WRAAAAAAFT",
        "Which way$to Denver?",
        "Do you know$the way to$San Jose?",
        "Do you$knowThe$Muffin Man",
        "Can we$fix it?",
        "What?$You cant$swim?",
        "Link.exe$has$stopped$working",
        "No running$by$the pool"
    ];

    public static readonly string[] BAGU_TEXTS =
    [
        "have you$seen error$around?",
        "tell the$riverman$i said hes$an idiot",
        "wanna see$a corpse?",
        "aliens$are real",
        "rupees are$mind$control$devices",
        "please dont$tell my$wife i am$here",
        "bam bam$bam",
        "Here is$my list of$demands",
        "my email to$River Man$was in$my drafts",
        "Hey!$Listen!",
        "Pizza$dudes got$thirty$seconds",
        "I am$Batman",
        "I am$Groot",
        "BAGU$SMAAAAASH",
        "Get out$of ma$swamp!!",
        "Praise$the sun",
        "am I$being$detained?",
        "Error$is the$evil twin",
        "Tingle$Tingle$Kooloo$Limpah!",
        "Is this a$pedestal$seed?",
        "Does$Spec rock$wear$glasses?",
        "Everyone$gets a$bridge"
    ];

    public static readonly string[] DOWNSTAB_TEXTS =
    [
        "stick them$with the$pointy end",
        "youll stab$your eye$out",
        "press down$you idiot",
        "have a$pogo stick",
        "yakhammer$acquired",
        "press down$to crouch",
        "kick punch$chop block$duck jump",
        "jump crouch$its all in$the mind!",
        "you walked$past me$didnt you",
        "upstab is$the best$stab",
        "Do the$Safety$Dance",
        "easy mode$activated",
        "Never$gonna give$you up",
        "Are you$Scrooge$McDuck?"
    ];

    public static readonly string[] UPSTAB_TEXTS =
    [
        "bet you$wish this$was$downstab",
        "you$probably$wont need$this",
        "press up$you idiot",
        "press up$to go in$doors",
        "are you$santa$claus?",
        "SHORYUKEN!",
        "you wasted$your time?",
        "Mario can$do this$without$magic",
        "downstab$is the$best stab",
        "Tiger$Uppercut!",
        "Never$gonna let$you down",
        "Thanks$for not$skipping$me",
        "The$Opportunity$Arises",
        "When you$up, press$jump to$stab"
    ];

    public static readonly string[] KNOW_NOTHING_TEXTS =
    [
        "I Know$Nothing",
        "Knowledge$Is Not$Mine",
        "I Like$Wasting$Your Time",
        "This Is$About As$Useful As$I Am",
        "Nothing$Know I",
        "Try To$Get A$Guide",
        "Git Gud",
        "What?$Yeah!$Okay!",
        "No hint$for you",
        "What$timeline$is this?",
        "your$call is$important$please hold",
        "silence$is golden",
        "Bless you",
        "PAY ME$AND I'LL$TALK",
        "the hint$is in$another$castle",
        "did you$check the$old kasuto$hint?",
        "Hookshot$is in$moon$palace",
        "Ness is$homesick",
        "Hi!$Billy Mays$here",
        "I like$turtles",
        "I hear$3D Zeldas$Have$SIX stabs",
        "I CANT TELL$YA HOW$NOTHING$I KNOW",
        "Password$is$Ken$sent me",
        "Why$are we$always$yelling?",
        "Rebo-shark$doot doot$doot doot$doot doot",
        "If I knew$something,$I would$tell you"
    ];

    public static readonly string[] MARIO_KNOW_NOTHING_TEXTS =
    [
        "Link, you$are looking$a little$red today.$Are you$well?",
        "Wow$you really$took the$wrong pipe",
        "You are$gonna save$Hyrule?$You dont$even have$a sword!",
        "With those$ears you$must be a$Gerudo.",
        "Are you$okay...$you look a$little red",
        "Mamma$mia",
        "Your$princess$is in$another$castle!",
        "I am sorry$but we do$not sell$mushrooms",
        "Bowser$sends$his$regards.",
        "You look$better in$green",
        "Do you$have a$cuter$younger$brother?",
        "Link was$looking for$you...$Did you$steal his$crystals?",
        "Did you$come here$by pipe or$by kart?",
        "Where is$Luigi$",
        "Where is$your sword",
        "Thank you$but your$princess$is in a$different$game",
        "Of course$I know you.$Everyone$knows$red Luigi!",
        "Jumping$without$the magic$words...$Thats not$fair.",
        "Your coins$are no$good here.",
        "Did you$get a$concussion$by hitting$your head$so often?",
        "What are$you?$a cop?",
        "The river$devil does$not like$plumbers.",
        "Get the$power.$Nintendo$power!",
        "I dont$talk with$plumbers.",
        "Where in$the world$is link?$He smashed$my pots$and left!",
        "Ganon is$so scary$please$save us!",
        "I$I said$I said this$I said this$to waste$your time.",
        "Welcome$stranger!$Hyrule is$a great$place to$live.",
        "I am$surprised$that we$speak the$same$language",
        "...",
        "Well$excuuuuuuse$me, Mario!",
        "Where did$you learn$to jump$like that?",
        "$$$$$... hi",
        "Save our$town and$i will let$you break$all our$pots!",
        "Are you$gonna save$us or what?",
        "So you$saved us.$$Big deal.",
        "Stop by$my shop$sometime.$Oh, you$dont have$any rupees.",
        "We welcome$all heroes.",
        "Take a$minute to$enjoy the$scenery",
        "Hope you$have a fun$stay in$our town!",
        "Why dont$you have$an outline?",
        "How can$your neck$support$that big$head?",
        "Try out$the Zelda 2$randomizer!",
        "Try out$other hacks$by jroweboy",
        "Go save$your own$princess,$bub.",
        "Wish I$could get$as strong$as you",
        "Isnt it$cheating$to start$with$downstab?",
        "Link had$to work$to earn$those$crystals$you know...",
        "Why is it$a triforce$if there$is only one$triangle?",
        "I am a$big fan of$jump man.",
        "What ever$happened$to your$famous$hotel chain",
        "Woah, not$everyday$you see$a man with$your build.",
        "Gotta say,$Zelda is a$cooler name$than Peach.",
        "Welcome!",
        "Hello$there!",
        "Please$stay as$long as you$need!",
        "I am in$a hurry. No$time to$explain!",
        "Time lost.$Reset your$speedrun.",
        "I always$feel like$ganon is$watching me",
        "The eyes$of Bowser$are$nowhere.",
        "The eyes$of Ganon$are$everywhere.",
        "So long,$I am off$to save$hyrule!",
        "Oh, didnt$see you$there!",
        "I am much$too busy$to talk$right now.",
        "I heard$Kasuto is$not doing$so well.",
        "Our towns$are named$after the$heroes of$old",
        "Is there$even a king$in the$mushroom$kingdom?",
    ];

    public static readonly string[] KNOW_NOTHING_OUTSIDE_TEXTS =
    [
        .. KNOW_NOTHING_TEXTS,
        "I am not$a vire$in$disguise",
        "Im trying$my best to$not turn$into an$ache",
    ];

    public static readonly string[] KNOW_NOTHING_KID_TEXTS =
    [
        .. KNOW_NOTHING_TEXTS,
        "They call$me the$hintless$kid",
    ];

    public static readonly string[] KNOW_NOTHING_SARIA_GREETER_TEXTS =
    [
        .. KNOW_NOTHING_OUTSIDE_TEXTS,
        "Whats the$deal with$pots?  You$Break them$and no one$notices!",
        "Whats the$deal with$lava?$Jump in,$refill MP,$no problem!",
    ];

    public static readonly string[] MOVING_NPC_TEXTS =
    [
        "I Know$Nothing",
        "Knowledge$Is Not$Mine",
        "Nothing$Know I",
        "Try To$Get A$Guide",
        "What$timeline$is this?",
        "Bless you",
        "Hola!",
        "I am not$a vire$in$disguise",
        "Woah!$Dude!",
        "Ness is$homesick",
        "Hi!$Billy Mays$here",
        "I like$turtles",
        "I hear$3D Zeldas$Have$SIX stabs",
        "I CANT TELL$YA HOW$NOTHING$I KNOW",
        "Password$is$Ken$sent me",
        "Why$are we$always$yelling?",
        "Im trying$my best to$not turn$into an$ache",
        "If I knew$something,$I would$tell you",
        "Im running$a swordless$seed",
    ];

    public static readonly string[] ALREADY_HAVE_ITEM_TEXTS =
    [
        "sorry,$an impostor$was here$earlier and$took it",
        "what more$do you want$from me?",
    ];

    public static readonly string[] NOT_ENOUGH_CONTAINERS_TEXTS =
    [
        "all signs$point to$no",
        "come back$as$adult link",
        "quit$wasting$my time",
        "Youre$sixteen$pixels$short",
        "Do you$have a$diploma?",
        "The magic$class did$not help$you enough",
        "Show me$your$credits!",
        "I cannot$contain$my laughter",
        "You must$construct$additional$pylons",
        "bet you$forgot$this flag$was on",
        "I'll tell$you when$you're$older"
    ];

    //If a static hint location (like downstab/upstab) is hinting for an item you already started with
    //the vanilla text can be misleading, so put in a more obvious replacement.
    public static string NON_COMMUNITY_STARTED_WITH_TEXT = "YOU STARTED$WITH$%%";

    public static readonly string[] STARTED_WITH_ITEM_TEXTS = 
    [
        "check your$tracker$for$%%",
        "%%$is on$the menu",
        "press$start for$%%",
        "you forgot$to mark$%%$on your$tracker",
        "you have$already$mastered$%%",
        "i wish$i had a$%%$like yours"
    ];

    public static readonly Dictionary<TownType, string[]> WIZARD_SPELL_TEXTS_BY_TOWN = new()
    {
        { TownType.RAURU, [] },
        { TownType.RUTO, ["A winner$is you"] },
        { TownType.SARIA, ["Water$you$doing?"] },
        { TownType.MIDO, [] },
        { TownType.NABOORU, ["Do not$drink the$fountain$water here$...$trust me."] },
        { TownType.DARUNIA, ["You saved$a kid$for this?", "Dont$forget to$get upstab"] },
        { TownType.NEW_KASUTO, [] },
        { TownType.OLD_KASUTO, ["Sorry$about the$moas"] },
        { TownType.BAGU, BAGU_TEXTS }
    };

    public static readonly Dictionary<Collectable, string[]> WIZARD_SPELL_TEXTS_BY_COLLECTABLE = new()
    {
        { Collectable.SHIELD_SPELL, ["Have you$tried not$dying?", "I Already$Have One", "Is This$A Red$Ring?"] },
        { Collectable.JUMP_SPELL, ["I get up$and nothin$gets me$down", "Kris Kross$will make$you..."] },
        { Collectable.TANOOKI, [] },
        { Collectable.LIFE_SPELL, ["have you$tried the$Healmore$spell?", "Dont$blame me$if this is$1 bar", "How Many$Bars Will$I Heal"] },
        { Collectable.FAIRY_SPELL, ["HEY!$LISTEN", "Just$dont say$Hey$listen!", "Watch Out$For Iron"] },
        { Collectable.FIRE_SPELL, ["this is$fine", "use this$to burn$gems", "This spell$is$worthless", "Goodness$Gracious!", "This one$goes out$to the one$I love"]},
        { Collectable.DASH_SPELL, ["Rolling$around at$the speed$of sound", "Gotta$Go$Fast", "Use the$boost to$get through"]},
        { Collectable.REFLECT_SPELL, ["I am not$Mirror$Shield", "Crysta$was$here", "Youre$rubber,$Theyre$glue", "Send$Carock my$regards", "Is This$Hera$Basement?"]},
        { Collectable.SPELL_SPELL, ["Titular$redundancy$included", "Wait?$which$spell?", "you should$rescue me$instead of$Zelda", "Can you$use it$in a$sentence?", "Metamorph$Thy Enemy", "How do you$Spell$Spell$Spell"]},
        { Collectable.THUNDER_SPELL, ["With this$you can$now beat$the game", "Ultrazord$Power Up!", "Terrible$terrible$damage", "hes dead$jim"]},
    };

    public static string DEFAULT_WIZARD_COLLECTABLE = "YOU GOT$THE$%%";

    public static string[] COMMUNITY_NONSPELL_GET_TEXT =
    [
        "GET$EQUIPPED$WITH THE$%",
        "Tis a good$Day for$%%",
        "I can't$believe$it's$%%",
        "All Hail$%%$heroes$rise again",
        "Congrats!$it's a$%%",
        "One Fish$Two Fish$Red Fish$%%",
        "Master it$and you$can have$%%",
        "%%$it's what$plants$crave",
        "%%$bagu$gives it$5 stars",
        "The$power of$%%$is yours",
        "ganon is$jealous$of your$%%",
        "the secret$to life is$%%",
        "Your love$is like$bad$%%",
        "Screw the$rules I$have this$%%",
        "excuse me$you forgot$this$%%",
        "takes this!$%%$now I can$retire",
        "you$gotta$have$%%",
        "I love the$%%$its so bad",
        "don't feed$%%$after$midnight",
        "take the$%%$leave the$cannoli",
        "%$%$%$Yay!",
        "I will$give you$%%$to go away",
        "%%$does not$spark joy",
        "lets talk$about$%%$baby!",
        "When all$else fails$try$%%",
        "%%$it's what's$for dinner",
        "%%$needs food$badly",
        "%%$detected",
        "Badger$Badger$Badger$%%",
        "The$World$is a$%%",
        "link$meets$%%",
        "a brand$new$%%$!!!",
        "Earth Fire$wind water$%%",
        "happy$%%$day!!",
        "%%$%%$never$changes",
        "%%$is$cheating",
        "This seed$sponsored$by$%%",
        "we all$live in$a yellow$%%",
        "its$%%$time!",
        "fresh$%%$50 rupees$obo",
        "no whammy$no whammy$and stop!$%%",
        "all you$need is$%%",
        "the secret$word is$%%",
        "have a$%%$on the$house",
        "I summon$%%$in attack$position!",
        "It puts the$%%$on its$skin",
        "I can haz$%%",
        "I know$this is$the$%%",
        "Smells$like$%%$in here",
        "Have this$%$But I will$ask a$favor of$you later",
        "I knew it!$%%$is the one.",
        "Zora has$%$cheaper$but this$saves on$shipping",
        "%%$gon give$it to ya",
        "Promise me$you will$use$%%$responsibly",
        "Try using$%%$at your$next party",
    ];

    public List<Text> Texts { get; private set; }

    public Text GetText(DialogWest index)
    {
        return Texts[(int)index];
    }
    public void SetText(DialogWest index, Text text)
    {
        Texts[(int)index] = text;
    }
    public Text GetText(DialogEast index)
    {
        return Texts[(int)index + WEST_TEXT_COUNT];
    }
    public void SetText(DialogEast index, Text text)
    {
        Texts[(int)index + WEST_TEXT_COUNT] = text;
    }

    public CustomTexts(ROM rom)
    {
        Texts = rom.GetGameText();
    }

    public CustomTexts(List<Text> texts)
    {
        Texts = texts;
    }

    public static List<Text> GenerateTexts(
        IEnumerable<Location> locations,
        List<Text> texts,
        RandomizerProperties props,
        Random hashRNG)
    {
        DebugValidateTexts();
      
        //Change the "vanilla" spell texts to mario specific texts
        if (props.MarioMode)
        {
            // In Mario Mode, change a few spell hint text to reflect its new ability
            texts[wizardTextIndexesBySpell[Collectable.JUMP_SPELL]] = new Text("JUMP WONT$HELP YOU.$TRY THIS$INSTEAD.");
            texts[downstabGuyGotItemTextIndex] = new Text("PIERCE$SHIELDS$WITH YOUR$STOMP.");
            texts[upstabGuyGotItemTextIndex] = new Text("BREAK BLOCKS$WITH HEAD$TO CREATE$POW-ERFUL$SHOCKWAVE");
            texts[wizardTextIndexesBySpell[Collectable.FIRE_SPELL]] = new Text("THIS MAGIC$WILL MAKE$YOUR HAND$THROW FIRE");
            texts[wizardTextIndexesBySpell[Collectable.SPELL_SPELL]] = new Text("SAY THE$MAGIC WORD$AND FEEL$THE POWER");
        }

        // This will use the `texts` list internally. A compromise because I shouldn't refactor this entire class at the moment,
        // and I can still use proper east indexes.
        CustomTexts customTexts = new(texts);

        // Make a new RNG for community text so that it doesn't affect the final hash.
        var nonhashRNG = new Random(hashRNG.Next());
        int textAttempts = 0;
        do
        {
            if (props.ReplaceFireWithDash)
            {
                texts[70] = new Text("USE THIS$TO GO$FAST");
            }

            props.ArchipelagoItemTextSlots = [];
            GenerateTownsfolkTexts(texts, locations, nonhashRNG, props.UseCommunityText, props.IncludeQuestItemsInShuffle, props);
            props.ArchipelagoSpellHintSlots = [];

            if (props.SpellItemHints)
            {
                GenerateSpellHints(locations, texts, props);
            }

            if (props.BagusWoods)
            {
                Location baguLocation = locations.FirstOrDefault(i => i.Town?.Type == TownType.BAGU)!
                    ?? throw new Exception("Bagu location not found while generating text");
                texts[riverManTextIndex] = GenerateBaguWoodsHint(baguLocation);
            }
            else if (props.UseCommunityText)
            {
                texts[riverManTextIndex] = GenerateRiverManText(nonhashRNG);
            }

            if (props.SpellItemHints && props.IncludeSwordTechsInShuffle)
            {
                Location? downstabLoc = locations.FirstOrDefault(i => i.GetAllCollectables().Contains(Collectable.DOWNSTAB));
                Location? upstabLoc = locations.FirstOrDefault(i => i.GetAllCollectables().Contains(Collectable.UPSTAB));
                Text hint;
                if (props.StartWithDownstab)
                {
                    hint = props.UseCommunityText ? new Text(STARTED_WITH_ITEM_TEXTS.Sample(nonhashRNG)!, Collectable.DOWNSTAB)
                        : new Text(NON_COMMUNITY_STARTED_WITH_TEXT, Collectable.DOWNSTAB);
                }
                else
                {
                    hint = downstabLoc == null && props.ArchipelagoMode
                        ? ArchipelagoSpellHint(props, Collectable.DOWNSTAB, downstabClosedDoorTextIndex)
                        : Text.GenerateHelpfulHint(downstabLoc!, Collectable.DOWNSTAB, props.HelpfulHints == HelpfulHintOption.TOWNS_SEPARATE, HELPFUL_HINTS_TREAT_BAGU_AS_TOWN);
                }
                texts[downstabClosedDoorTextIndex] = hint;
                if (props.StartWithUpstab)
                {
                    hint = props.UseCommunityText ? new Text(STARTED_WITH_ITEM_TEXTS.Sample(nonhashRNG)!, Collectable.UPSTAB)
                        : new Text(NON_COMMUNITY_STARTED_WITH_TEXT, Collectable.UPSTAB);
                }
                else
                {
                    hint = upstabLoc == null && props.ArchipelagoMode
                        ? ArchipelagoSpellHint(props, Collectable.UPSTAB, upstabClosedDoorTextIndex)
                        : Text.GenerateHelpfulHint(upstabLoc!, Collectable.UPSTAB, props.IncludeSpellsInShuffle, HELPFUL_HINTS_TREAT_BAGU_AS_TOWN);
                }
                texts[upstabClosedDoorTextIndex] = hint;
                if (props.SwapUpAndDownStab)
                {
                    (texts[upstabClosedDoorTextIndex], texts[downstabClosedDoorTextIndex]) 
                        = (texts[downstabClosedDoorTextIndex], texts[upstabClosedDoorTextIndex]);
                    // the placeholders moved with the texts
                    foreach (var stab in props.ArchipelagoSpellHintSlots.Keys.ToList())
                    {
                        int at = props.ArchipelagoSpellHintSlots[stab];
                        if (at == upstabClosedDoorTextIndex) props.ArchipelagoSpellHintSlots[stab] = downstabClosedDoorTextIndex;
                        else if (at == downstabClosedDoorTextIndex) props.ArchipelagoSpellHintSlots[stab] = upstabClosedDoorTextIndex;
                    }
                }
            }

            if (props.ArchipelagoMode && props.HelpfulHints != HelpfulHintOption.NONE)
            {
                List<int> placedIndexes = GenerateArchipelagoHintSlots(texts, hashRNG, props);
                GenerateKnowNothings(texts, placedIndexes, nonhashRNG, props.HelpfulHints, props.BagusWoods, props.UseCommunityText, props.MarioMode);
            }
            else if (props.HelpfulHints != HelpfulHintOption.NONE || props.MarioMode)
            {
                List<int> placedIndexes = GenerateHelpfulHints(texts, locations, hashRNG, props);
                GenerateKnowNothings(texts, placedIndexes, nonhashRNG, props.HelpfulHints, props.BagusWoods, props.UseCommunityText, props.MarioMode);
            }

            if (props.UseCommunityText)
            {
                //Generate replacements for "I CANNOT HELP YOU ANYMORE. GO NOW."
                customTexts.SetText(DialogWest.ALREADY_HAVE_ITEM, new Text(ALREADY_HAVE_ITEM_TEXTS.Sample(nonhashRNG)!));
                customTexts.SetText(DialogEast.ALREADY_HAVE_ITEM, new Text(ALREADY_HAVE_ITEM_TEXTS.Sample(nonhashRNG)!));
                //Generate replacements for "COME BACK WHEN YOU ARE READY" that is displayed when
                //you don't have enough magic containers and container requirements are on.
                customTexts.SetText(DialogEast.NOT_ENOUGH_CONTAINERS, new Text(NOT_ENOUGH_CONTAINERS_TEXTS.Sample(nonhashRNG)!));
            }
            customTexts.SetText(DialogEast.OLD_KASUTO_NOT_ENOUGH_CONTAINERS, customTexts.GetText(DialogEast.NOT_ENOUGH_CONTAINERS));

            if (props.TownNameHints)
            {
                GenerateTownNameHints(texts, locations, props);
            }
        } while (TextLength(texts) > MAX_TEXT_LENGTH && ++textAttempts < MAX_TEXT_ATTEMPTS);

        if (TextLength(texts) > MAX_TEXT_LENGTH)
        {
            // Archipelago placeholders are a fixed size, so a seed that is already near the limit can never shrink under it
            throw new ImpossibleException($"The dialog did not fit in {MAX_TEXT_LENGTH} bytes after {MAX_TEXT_ATTEMPTS} attempts");
        }

        return texts;
    }

    private static Text GenerateBaguWoodsHint(Location bagu)
    {
        int baguy = bagu.Y;
        int bagux = bagu.Xpos;
        string hint = "BAGU IN$";
        if (baguy < 25)
        {
            if (bagux < 21)
            {
                hint += "NORTHWEST$";
            }
            else if (bagux < 42)
            {
                hint += "NORTH$";
            }
            else
            {
                hint += "NORTHEAST$";
            }
        }
        else if (baguy < 50)
        {
            if (bagux < 21)
            {
                hint += "WEST$";
            }
            else if (bagux < 42)
            {
                hint += "CENTER";
            }
            else
            {
                hint += "EAST$";
            }
        }
        else
        {
            if (bagux < 21)
            {
                hint += "SOUTHWEST$";
            }
            else if (bagux < 42)
            {
                hint += "SOUTH$";
            }
            else
            {
                hint += "SOUTHEAST$";
            }
        }
        hint += "WOODS";
        Text baguHint = new Text(hint);
        return baguHint;
    }

    private static void GenerateTownNameHints(List<Text> texts, IEnumerable<Location> locations, RandomizerProperties props)
    {
        bool linkedFire = props.LinkedFireSpell != null;
        foreach (Location location in locations.Where(i => i.Town?.GetWizard() != null))
        {
            if(location?.Town?.Type != null && TOWN_SIGN_INDEXES.TryGetValue((TownType)location?.Town?.Type!, out int index))
            {
                // In Archipelago mode, a wizard in the pool holds an AP item, which isn't known until the multiworld
                // is generated. The sign gets a placeholder that the Archipelago client rewrites in game.
                if (props.ArchipelagoMode && ArchipelagoItems.IsInPool(location.Town!.GetWizard()!, location.Town, props))
                {
                    texts[index] = ArchipelagoSignPlaceholder((TownType)location.Town.Type!);
                    continue;
                }
                texts[index] = GenerateTownSignHint((Collectable)location.Town.GetWizard()!.Collectable!, linkedFire, props.ArchipelagoMode);
            }
        }
    }

    /// <summary>
    /// Bytes reserved for an Archipelago helpful hint: the longest place ("carock$disappears$with the$", 27 characters)
    /// plus an item name of two 10 character lines (21), the end byte, and one byte holding 0x80 + the hint number.
    /// </summary>
    public const int ARCHIPELAGO_HINT_SIZE = 52;

    /// <summary>
    /// In Archipelago mode the items aren't known when the ROM is made, so helpful hints are placeholders the
    /// Archipelago client fills in. This picks the NPCs that give hints, the same way GenerateHelpfulHints does (one in
    /// each of five towns, always one in Old Kasuto), and records them in props.ArchipelagoHintSlots.
    /// </summary>
    private static List<int> GenerateArchipelagoHintSlots(List<Text> hints, Random r, RandomizerProperties props)
    {
        List<int> placedIndex = [];
        props.ArchipelagoHintSlots = [];
        // the usual hint count plus the one a seed with spells in the shuffle gets for a critical spell
        int hintsCount = HELPFUL_HINTS_COUNT + 1;
        const int oldKasuto = 7;
        List<int> towns = Enumerable.Range(0, hintIndexes.Length).Where(t => t != oldKasuto).ToList();
        towns.FisherYatesShuffle(r);
        towns = [oldKasuto, .. towns.Take(hintsCount - 1)];

        for (int slot = 0; slot < towns.Count; slot++)
        {
            int index = hintIndexes[towns[slot]][r.Next(hintIndexes[towns[slot]].Length)];
            int[] indexes = index switch
            {
                errorTextIndex1 => [errorTextIndex1, errorTextIndex2],
                talkingBotIndexTalking => [talkingBotIndexSleeping, talkingBotIndexTalking],
                talkingAcheIndexTalking => [talkingAcheIndexSleeping, talkingAcheIndexTalking],
                _ => [index],
            };
            Text placeholder = ArchipelagoHintPlaceholder(slot);
            foreach (int i in indexes)
            {
                hints[i] = placeholder;
                placedIndex.Add(i);
            }
            props.ArchipelagoHintSlots.Add(indexes);
        }
        return placedIndex;
    }

    /// <summary>
    /// A helpful hint reading "AP HINT", padded with end bytes to ARCHIPELAGO_HINT_SIZE. The last byte is 0x80 + the
    /// hint number, which keeps each hint unique when the ROM is assembled. The Archipelago client
    /// (worlds/z2r/Client.py) relies on this layout.
    /// </summary>
    public static Text ArchipelagoHintPlaceholder(int slot)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xFF, ARCHIPELAGO_HINT_SIZE).ToArray();
        Util.ToGameText("AP HINT", true).CopyTo(bytes, 0);
        bytes[^1] = (byte)(0x80 + slot);
        return new Text(bytes);
    }

    /// <summary>
    /// Bytes reserved for a spell item hint the Archipelago client fills in: four 11 character lines ("TROPHY AT",
    /// the player, two lines of location), three line breaks, the end byte, and one byte holding 0xA0 + the item's
    /// number (the client relies on this layout).
    /// </summary>
    public const int ARCHIPELAGO_SPELL_HINT_SIZE = 49;

    /// <summary>The order of the spell hint items, which gives each placeholder its last byte.</summary>
    public static readonly Collectable[] ARCHIPELAGO_SPELL_HINT_ITEMS =
        [Collectable.TROPHY, Collectable.MEDICINE, Collectable.CHILD, Collectable.MIRROR, Collectable.WATER,
         Collectable.DOWNSTAB, Collectable.UPSTAB];

    /// <summary>
    /// A spell item hint for an item Archipelago places: "AP HINT" padded with end bytes to
    /// ARCHIPELAGO_SPELL_HINT_SIZE, with 0xA0 + the item's number last. Records its text index in
    /// props.ArchipelagoSpellHintSlots.
    /// </summary>
    private static Text ArchipelagoSpellHint(RandomizerProperties props, Collectable item, int textIndex)
    {
        props.ArchipelagoSpellHintSlots[item] = textIndex;
        return ArchipelagoSpellHintPlaceholder(item);
    }

    public static Text ArchipelagoSpellHintPlaceholder(Collectable item)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xFF, ARCHIPELAGO_SPELL_HINT_SIZE).ToArray();
        Util.ToGameText("AP HINT", true).CopyTo(bytes, 0);
        bytes[^1] = (byte)(0xA0 + Array.IndexOf(ARCHIPELAGO_SPELL_HINT_ITEMS, item));
        return new Text(bytes);
    }

    /// <summary>
    /// Bytes reserved for the item-get dialog of a town location the Archipelago client fills in: four 11 character
    /// lines, three line breaks, the end byte, and one byte holding 0xC0 + the dialog's number (the client relies on
    /// this layout).
    /// </summary>
    public const int ARCHIPELAGO_ITEM_TEXT_SIZE = 49;

    /// <summary>"AP ITEM" padded with end bytes to ARCHIPELAGO_ITEM_TEXT_SIZE, with 0xC0 + the dialog's number last.</summary>
    public static Text ArchipelagoItemTextPlaceholder(int number)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xFF, ARCHIPELAGO_ITEM_TEXT_SIZE).ToArray();
        Util.ToGameText("AP ITEM", true).CopyTo(bytes, 0);
        bytes[^1] = (byte)(0xC0 + number);
        return new Text(bytes);
    }

    /// <summary>
    /// The start of a helpful hint about an item at this location, such as "go west to$find the$", which the
    /// Archipelago client finishes with the item's name.
    /// </summary>
    public static string ArchipelagoHintPlace(Location location, RandomizerProperties props)
    {
        return Text.HelpfulHintPlace(location, props.HelpfulHints == HelpfulHintOption.TOWNS_SEPARATE, HELPFUL_HINTS_TREAT_BAGU_AS_TOWN);
    }

    /// <summary>
    /// Bytes reserved for a town sign the Archipelago client rewrites: the longest sign it can write
    /// ("MAGIC$CONTAINER$TOWN", 20 characters) plus its end byte, plus one byte holding the town type.
    /// </summary>
    public const int ARCHIPELAGO_SIGN_SIZE = 22;

    /// <summary>
    /// A town sign reading "AP ITEM TOWN", padded with end bytes to ARCHIPELAGO_SIGN_SIZE so the client has room to
    /// write the item's name in place. The last byte is the town type, which keeps each town's sign unique (identical
    /// texts are merged when the ROM is assembled, and each sign must have its own bytes) and lets the client check it
    /// found the right sign. The Archipelago client (worlds/z2r/Client.py) relies on this layout.
    /// </summary>
    public static Text ArchipelagoSignPlaceholder(TownType town)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xFF, ARCHIPELAGO_SIGN_SIZE).ToArray();
        byte[] text = Util.ToGameText("AP$ITEM$town", true);
        text.CopyTo(bytes, 0);
        bytes[^1] = (byte)town;
        return new Text(bytes);
    }
    private static Text GenerateTownSignHint(Collectable spell, bool linkedFire, bool shortSpellNames)
    {
        string text = spell.EnglishText(shortSpellNames);
        if(spell == Collectable.FIRE_SPELL && linkedFire)
        {
            text = shortSpellNames ? "FIRE!" : "FIRE!$SPELL"; // Archipelago seeds use the short spell names
        }
        text += "$town";
        return new Text(text);
    }

    private static Text GenerateTownsfolkText(List<Text> texts, Random r, TownType town, TownMap townMap,
     bool useCommunityText, bool includeQuestItemsInShuffle, bool shortSpellNames = false)
    {
        Collectable collectable = townMap.Collectable ?? throw new Exception("Cannot generate hint for location with no collectable");
        switch (townMap.CollectableTextType)
        {
            //Wizards (and wizard-like trainers) use old spell texts if they have a spell, else newer collectable text
            case CollectableTextType.WIZARD:
            case CollectableTextType.DOWNSTAB_TRAINER:
            case CollectableTextType.UPSTAB_TRAINER:
                if (collectable.IsSpell())
                {
                    if (useCommunityText)
                    {
                        List<string> possibleWizardHints = WIZARD_SPELL_TEXTS_BY_TOWN[town].Union(WIZARD_SPELL_TEXTS_BY_COLLECTABLE[collectable]).ToList();
                        //For legacy reasons, bagu doesn't use generic text, but maybe he should just be a normal wizard
                        //like the trainers now are?
                        if (town != TownType.BAGU)
                        {
                            possibleWizardHints.Add(GENERIC_WIZARD_TEXTS);
                        }
                        int selectedHintIndex = r.Next(possibleWizardHints.Count());
                        return new Text(possibleWizardHints[selectedHintIndex]);
                    }
                    //Non community-text spells use the vanilla text corresponding to the spell you get.
                    return new Text(texts[wizardTextIndexesBySpell[collectable]].RawText);
                }
                return GenerateNonSpellTownsfolkText(collectable, r, useCommunityText, shortSpellNames);
            case CollectableTextType.BAGU:
                return GenerateBaguText(collectable, r, useCommunityText, includeQuestItemsInShuffle, shortSpellNames)
                    ?? new Text(texts[baguTextIndex].RawText);
            case CollectableTextType.TABLE:
                return GenerateMirrorTableText(collectable, r, useCommunityText, includeQuestItemsInShuffle, shortSpellNames)
                    ?? new Text(texts[gotMirrorTextIndex].RawText);
            case CollectableTextType.FOUNTAIN:
                return GenerateFountainText(collectable, r, useCommunityText, includeQuestItemsInShuffle, shortSpellNames)
                    ?? new Text(texts[gotWaterTextIndex].RawText);
            default:
                throw new Exception("Unrecognized CollectableTextType while generating hint");
        }
    } 

    private static Text? GenerateBaguText(Collectable baguItem, Random r, bool useCommunityText, bool includeQuestItemsInShuffle, bool shortSpellNames)
    {
        if(includeQuestItemsInShuffle)
        {
            return GenerateNonSpellTownsfolkText(baguItem, r, useCommunityText, shortSpellNames);
        }
        else if(useCommunityText)
        {
            return new Text(BAGU_TEXTS.Sample(r)!);
        }
        return null;
    }
    private static Text? GenerateMirrorTableText(Collectable mirrorItem, Random r, bool useCommunityText, bool includeQuestItemsInShuffle, bool shortSpellNames)
    {
        if (includeQuestItemsInShuffle)
        {
            if (useCommunityText)
            {
                return GenerateNonSpellTownsfolkText(mirrorItem, r, true, shortSpellNames);
            }
            else {
                string rawText;
                rawText = "I FOUND A$%$UNDER THE$TABLE.";
                    return new Text(rawText.Replace("%", mirrorItem.SingleLineText()));
            }
        }
        return null;
    }

    private static Text? GenerateFountainText(Collectable fountainItem, Random r, bool useCommunityText, bool includeQuestItemsInShuffle, bool shortSpellNames)
    {
        if (includeQuestItemsInShuffle)
        {
            return GenerateNonSpellTownsfolkText(fountainItem, r, useCommunityText, shortSpellNames);
        }
        return null;
    }
    private static Text GenerateRiverManText(Random r)
    {
        return new Text(RIVER_MAN_TEXTS[r.Next(RIVER_MAN_TEXTS.Length)]);
    }

    private static void GenerateKnowNothings(List<Text> hints, List<int> placedIndexes, Random r, HelpfulHintOption helpfulHintOption, bool useBaguWoods, bool useCommunityText, bool marioMode)
    {
        string[][] allTexts = [
            KNOW_NOTHING_KID_TEXTS,
            KNOW_NOTHING_SARIA_GREETER_TEXTS,
            KNOW_NOTHING_OUTSIDE_TEXTS,
            KNOW_NOTHING_TEXTS,
            MOVING_NPC_TEXTS
        ];
        var maybeMarioAdded = marioMode ?
            allTexts.Select(list => (string[])[..MARIO_KNOW_NOTHING_TEXTS, ..list]).ToArray()
            : allTexts;
        ShuffleBag<string> marioOnlyTexts = new(MARIO_KNOW_NOTHING_TEXTS, r);
        ShuffleBag<string> kidKnowNothingTexts = new(maybeMarioAdded[0], r);
        ShuffleBag<string> sariaGreeterTexts = new(maybeMarioAdded[1], r);
        ShuffleBag<string> outsideKnowNothingTexts = new(maybeMarioAdded[2], r);
        ShuffleBag<string> knowNothingTexts = new(maybeMarioAdded[3], r);
        ShuffleBag<string> movingNpcTexts = new(maybeMarioAdded[4], r);

        List<int> stationary =
        [
            .. rauruHints,
            .. rutoHints,
            .. sariaHints,
            .. midoHints,
            .. nabooruHints,
            .. daruniaHints,
            .. newkasutoHints,
            .. kingsTomb,
            .. oldkasutoHint,
            errorTextIndex2,
            talkingAcheIndexTalking,
            talkingBotIndexTalking,
        ];

        List<int> moving =
        [
            .. genericEastMoving,
            .. genericWestMoving,
            .. rauruMoving,
            .. rutoMoving,
            .. sariaMoving,
            .. movingMido,
            .. movingNabooru,
            .. daruniaMoving,
            .. newkasutoMoving,
        ];

        Text defaultKnowNothing = new();
        
        // if we are doing mario mode, we could have helpful hints off but still want mario flavor text
        // for the moving NPCs, so skip stationary onces
        if (helpfulHintOption != HelpfulHintOption.NONE)
        {
            for (int i = 0; i < stationary.Count; i++)
            {
                int textIndex = stationary[i];
                if (!placedIndexes.Contains(textIndex))
                {
                    Text hint;
                    if (useCommunityText)
                    {
                        if (kidHintNpcs.Contains(textIndex))
                        {
                            hint = new Text(kidKnowNothingTexts.Draw());
                        }
                        else if (textIndex == SariaGreeterNpc)
                        {
                            hint = new Text(sariaGreeterTexts.Draw());
                        }
                        else if (outsideHintNpcs.Contains(textIndex))
                        {
                            hint = new Text(outsideKnowNothingTexts.Draw());
                        }
                        else
                        {
                            hint = new Text(knowNothingTexts.Draw());
                        }
                    }
                    else
                    {
                        hint = defaultKnowNothing;
                    }
                    hints[textIndex] = hint;
                    if (textIndex == talkingAcheIndexTalking)
                    {
                        hints[talkingAcheIndexSleeping] = hint;
                    }
                    else if (textIndex == talkingBotIndexTalking)
                    {
                        hints[talkingBotIndexSleeping] = hint;
                    }
                }
            }
        }

        for (int i = 0; i < moving.Count; i++)
        {
            hints[moving[i]] = useCommunityText ? new Text(movingNpcTexts.Draw()) : marioMode ? new Text(marioOnlyTexts.Draw()) : defaultKnowNothing;
        }
    }

    private static List<int> GenerateHelpfulHints(List<Text> hints, IEnumerable<Location> locations, Random r, 
        RandomizerProperties props)
    {
        List<int> placedIndex = [];

        List<Collectable> placedItems = [];

        List<int> placedTowns = [];

        List<Collectable> items = locations.SelectMany(i => i.GetAllCollectables()).ToList();
        items = items.Where(i => !i.IsInternalUse()).ToList();

        if (!props.IncludeSpellsInShuffle)
        {
            items.RemoveAll(o => o.IsSpell());
        }

        if (props.StartWithSpellItems || props.SpellItemHints)
        {
            items.Remove(Collectable.TROPHY);
            items.Remove(Collectable.CHILD);
            items.Remove(Collectable.MEDICINE);
        }

        if (!props.IncludeQuestItemsInShuffle || props.SpellItemHints)
        {
            items.Remove(Collectable.MIRROR);
            items.Remove(Collectable.WATER);
        }
        if (!props.IncludeQuestItemsInShuffle)
        {
            items.Remove(Collectable.BAGUS_NOTE);
        }

        if (!props.IncludeSwordTechsInShuffle || props.SpellItemHints)
        {
            items.Remove(Collectable.DOWNSTAB);
            items.Remove(Collectable.UPSTAB);
        }

        int hintsCount = HELPFUL_HINTS_COUNT;
        if(OLD_KASUTO_HINT_IS_ALWAYS_GOOD)
        {
            hintsCount++;
        }
        //trying without the bonus hint now that spell shuffle has town hints.
        /*
        if(props.IncludeSwordTechsInShuffle && props.IncludeQuestItemsInShuffle)
        {
            hintsCount++;
        }
        */
        List<Collectable> hintCollectables = items.Where(i => !smallItems.Contains(i)).ToList();
        hintCollectables.FisherYatesShuffle(r);

        if (props.IncludeSpellsInShuffle)
        {
            Collectable[] criticalSpells = [Collectable.THUNDER_SPELL, Collectable.FAIRY_SPELL, Collectable.REFLECT_SPELL];
            criticalSpells = criticalSpells.Where(i => hintCollectables.Contains(i)).ToArray();
            if(criticalSpells.Length > 0)
            {
                var collectableSpell = criticalSpells.Sample(r);
                hintCollectables.Remove(collectableSpell);
                hintCollectables.Insert(0, collectableSpell); // move important spell to the front so it's always in the taken subset
                hintsCount++;
            }
        }

        hintsCount = Math.Min(hintsCount, hintCollectables.Count);
        if (hintsCount > 0)
        {
            hintCollectables = hintCollectables.Take(hintsCount - 1).ToList();
        }
        hintCollectables.Add(items.Where(smallItems.Contains).ToList().Sample(r));
        hintCollectables.FisherYatesShuffle(r);

        bool requiredOldKasutoHintPlaced = !OLD_KASUTO_HINT_IS_ALWAYS_GOOD;
        foreach(Collectable hintCollectable in hintCollectables)
        {
            List<Location> possibleHintLocations = locations.Where(i => i.GetAllCollectables().Contains(hintCollectable)).ToList();

            Location hintLocation;
            int town;
            do
            {
                //This fails if we ever have more than 1 item location in old kasuto, but that is unlikely
                if(!hintCollectable.IsMinorItem() && !requiredOldKasutoHintPlaced)
                {
                    town = 7;
                    requiredOldKasutoHintPlaced = true;
                }
                else
                {
                    town = r.Next(9);
                }
                hintLocation = possibleHintLocations.Sample(r) ?? throw new ImpossibleException("Error generating hint for unplaced item");

            }
            //don't let hints be for items in the same town
            while ((hintLocation.Town != null && hintLocation.Town?.Type?.VanillaTownOrder() - 1 == town)
             || placedTowns.Contains(town));
            Text hint = Text.GenerateHelpfulHint(hintLocation, hintCollectable, props.HelpfulHints == HelpfulHintOption.TOWNS_SEPARATE, HELPFUL_HINTS_TREAT_BAGU_AS_TOWN, props.ArchipelagoMode);
            int index = hintIndexes[town][r.Next(hintIndexes[town].Length)];
            switch (index)
            {
                case errorTextIndex1:
                    hints[errorTextIndex1] = hint;
                    hints[errorTextIndex2] = hint;
                    placedIndex.Add(errorTextIndex1);
                    placedIndex.Add(errorTextIndex2);
                    break;
                case talkingBotIndexTalking:
                    hints[talkingBotIndexSleeping] = hint;
                    hints[talkingBotIndexTalking] = hint;
                    placedIndex.Add(talkingBotIndexSleeping);
                    placedIndex.Add(talkingBotIndexTalking);
                    break;
                case talkingAcheIndexTalking:
                    hints[talkingAcheIndexSleeping] = hint;
                    hints[talkingAcheIndexTalking] = hint;
                    placedIndex.Add(talkingAcheIndexSleeping);
                    placedIndex.Add(talkingAcheIndexTalking);
                    break;
                default:
                    hints[index] = hint;
                    placedIndex.Add(index);
                    break;
            }

            placedTowns.Add(town);
            placedItems.Add(hintCollectable);
        }
        return placedIndex;
    }

    private static void GenerateSpellHints(IEnumerable<Location> locations, List<Text> hints, RandomizerProperties props)
    {
        if(props.SpellItemHints && !props.StartWithSpellItems)
        {
            Location itemLocation;

            itemLocation = locations.FirstOrDefault(i => i.GetAllCollectables().Contains(Collectable.TROPHY))!;
            if(itemLocation != null)
            {
                Text trophyHint = Text.GenerateHelpfulHint(itemLocation, Collectable.TROPHY, props.HelpfulHints == HelpfulHintOption.TOWNS_SEPARATE, HELPFUL_HINTS_TREAT_BAGU_AS_TOWN);
                hints[trophySpellHintIndex] = trophyHint;
            }
            else if (props.ArchipelagoMode)
            {
                hints[trophySpellHintIndex] = ArchipelagoSpellHint(props, Collectable.TROPHY, trophySpellHintIndex);
            }

            itemLocation = locations.FirstOrDefault(i => i.GetAllCollectables().Contains(Collectable.MEDICINE))!;
            if (itemLocation != null)
            {
                Text medHint = Text.GenerateHelpfulHint(itemLocation, Collectable.MEDICINE, props.HelpfulHints == HelpfulHintOption.TOWNS_SEPARATE, HELPFUL_HINTS_TREAT_BAGU_AS_TOWN);
                hints[medicineSpellHintIndex] = medHint;
            }
            else if (props.ArchipelagoMode)
            {
                hints[medicineSpellHintIndex] = ArchipelagoSpellHint(props, Collectable.MEDICINE, medicineSpellHintIndex);
            }   

            itemLocation = locations.FirstOrDefault(i => i.GetAllCollectables().Contains(Collectable.CHILD))!;
            if (itemLocation != null)
            {
                Text kidHint = Text.GenerateHelpfulHint(itemLocation, Collectable.CHILD, props.HelpfulHints == HelpfulHintOption.TOWNS_SEPARATE, HELPFUL_HINTS_TREAT_BAGU_AS_TOWN);
                hints[childSpellHintIndex] = kidHint;
            }
            else if (props.ArchipelagoMode)
            {
                hints[childSpellHintIndex] = ArchipelagoSpellHint(props, Collectable.CHILD, childSpellHintIndex);
            }

            if (props.IncludeQuestItemsInShuffle)
            {
                itemLocation = locations.FirstOrDefault(i => i.GetAllCollectables().Contains(Collectable.MIRROR))!;
                if (itemLocation != null)
                {
                    Text mirrorHint = Text.GenerateHelpfulHint(itemLocation, Collectable.MIRROR, props.HelpfulHints == HelpfulHintOption.TOWNS_SEPARATE, HELPFUL_HINTS_TREAT_BAGU_AS_TOWN);
                    hints[mirrorSpellHintIndex] = mirrorHint;
                }
                else if (props.ArchipelagoMode)
                {
                    hints[mirrorSpellHintIndex] = ArchipelagoSpellHint(props, Collectable.MIRROR, mirrorSpellHintIndex);
                }

                itemLocation = locations.FirstOrDefault(i => i.GetAllCollectables().Contains(Collectable.WATER))!;
                if (itemLocation != null)
                {
                    Text waterHint = Text.GenerateHelpfulHint(itemLocation, Collectable.WATER, props.HelpfulHints == HelpfulHintOption.TOWNS_SEPARATE, HELPFUL_HINTS_TREAT_BAGU_AS_TOWN);
                    hints[waterSpellHintIndex] = waterHint;
                }
                else if (props.ArchipelagoMode)
                {
                    hints[waterSpellHintIndex] = ArchipelagoSpellHint(props, Collectable.WATER, waterSpellHintIndex);
                }
            }
        }
    }

    private static void GenerateTownsfolkTexts(List<Text> texts, IEnumerable<Location> itemLocs, Random r, 
        bool useCommunityText, bool includeQuestItemsInShuffle, RandomizerProperties? props = null)
    {
        List<Text> vanillaTexts = new(texts);
        List<Text> usedTexts = [];

        foreach (Location townLocation in itemLocs.Where(i => i.Town != null))
        {
            Town town = townLocation.Town!;
            TownType type = town.Type ?? throw new Exception("Town missing type while generating hints");
            foreach(TownMap map in town.TownMaps.Where(m => m.CollectableTextType != null))
            {
                Text text;
                int tries = 0;
                do
                {
                    text = GenerateTownsfolkText(vanillaTexts, r, type, map, useCommunityText, includeQuestItemsInShuffle, props?.ArchipelagoMode ?? false);
                } while (useCommunityText && usedTexts.Contains(text) && tries++ < TOWNSFOLK_TEXT_ATTEMPT_LIMIT);
                if(tries > TOWNSFOLK_TEXT_ATTEMPT_LIMIT)
                {
                    logger.Warn("Exceeded townsfolk text generation attempt limit. Using error text");
                    text = new(ERROR_TEXT);
                }
                usedTexts.Add(text);
                //TODO (eventually): This will for sure break as soon as we start doing custom towns
                //Figure out a better way to index this, or just make some kind of global check id.
                int textIndex = townsfolkTextIndexes[map.Map];
                if (props != null && props.ArchipelagoMode && map.Collectable == Collectable.AP_ITEM)
                {
                    // the item isn't known until the multiworld is, so the client fills this dialog in
                    string nodeId = GraphNodeIds.TownMap(townLocation, map);
                    int number = props.ArchipelagoItemTextSlots.Count;
                    text = ArchipelagoItemTextPlaceholder(number);
                    props.ArchipelagoItemTextSlots[nodeId] = (textIndex, number);
                }
                texts[textIndex] = text;
            }
        }
    }

    private static Text GenerateNonSpellTownsfolkText(Collectable collectable, Random r, bool useCommunityText, bool shortSpellNames)
    {
        string collectableWizardText;

        if(useCommunityText)
        {
            collectableWizardText = COMMUNITY_NONSPELL_GET_TEXT[r.Next(COMMUNITY_NONSPELL_GET_TEXT.Length)];
        }
        else
        {
            collectableWizardText = DEFAULT_WIZARD_COLLECTABLE;
        }

        return new Text(collectableWizardText, collectable, shortSpellNames);
    }

    private static int TextLength(List<Text> texts)
    {
        // assume js65 will optimize texts if they are the same, so ignore duplicates here
        HashSet<Text> uniqueTexts = new();

        int sum = 0;
        for (int i = 0; i < texts.Count(); i++)
        {
            var t = texts[i];
            if (uniqueTexts.Add(t))
            {
                sum += t.EncodedText.Length;
            }
        }
        return sum;
    }

    public static List<Text> BuildMovingNpcDialogPool(RandomizerProperties props)
    {
        List<string> raw = [];
        if (props.UseCommunityText)
        {
            raw.AddRange(MOVING_NPC_TEXTS);
            raw.AddRange(KNOW_NOTHING_TEXTS);
        }
        if (props.MarioMode)
        {
            raw.AddRange(MARIO_KNOW_NOTHING_TEXTS);
        }

        // Dedup by string (preserving first-seen order) so no line is stored twice in ROM.
        List<Text> pool = [.. raw.Distinct().Select(s => new Text(s))];
        // Trim the list of texts back until it fits in the max size given
        while (pool.Count > 1 && MovingNpcDialogByteLength(pool) > MOVING_NPC_DIALOG_MAX_BYTES)
        {
            pool.RemoveAt(pool.Count - 1);
        }

        return pool;
    }

    private static int MovingNpcDialogByteLength(List<Text> pool)
    {
        const int rollRoutineBytes = 24;
        int stringBytes = pool.Sum(t => t.EncodedText.Length);
        int pointerTableBytes = pool.Count * 2;
        return stringBytes + pointerTableBytes + rollRoutineBytes;
    }

    /// Make sure all strings are max 11x6
    [Conditional("DEBUG")]
    private static void DebugValidateTexts()
    {
        List<string> allTexts = [
            .. GENERIC_WIZARD_TEXTS,
            .. RIVER_MAN_TEXTS,
            .. BAGU_TEXTS,
            .. DOWNSTAB_TEXTS,
            .. UPSTAB_TEXTS,
            .. KNOW_NOTHING_TEXTS,
            .. KNOW_NOTHING_OUTSIDE_TEXTS,
            .. KNOW_NOTHING_KID_TEXTS,
            .. KNOW_NOTHING_SARIA_GREETER_TEXTS,
            .. MOVING_NPC_TEXTS,
            .. ALREADY_HAVE_ITEM_TEXTS,
            .. NOT_ENOUGH_CONTAINERS_TEXTS,
            .. COMMUNITY_NONSPELL_GET_TEXT,

            .. WIZARD_SPELL_TEXTS_BY_TOWN.SelectMany(ls => ls.Value),
            .. WIZARD_SPELL_TEXTS_BY_COLLECTABLE.SelectMany(ls => ls.Value),
        ];

        foreach (var s in allTexts)
        {
            var text = new Text(s);
            Debug.Assert(text.ValidateDialogText());
        }
    }
}

