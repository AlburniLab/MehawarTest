#nullable enable
using System.Collections.Generic;

namespace Mehawar.Greybox
{
    /// <summary>One line of an interlude. Voice lines (Via Oscura) are the intrusion: rendered
    /// in the empty-violet palette, italic, and they arrive AFTER the memory, not with it.</summary>
    public sealed class InterludeLine
    {
        public InterludeLine(string text, bool isVoice = false)
        {
            Text = text;
            IsVoice = isVoice;
        }

        public string Text { get; }
        public bool IsVoice { get; }
    }

    /// <summary>A between-levels narrative beat. Data only — the screen renders it.</summary>
    public sealed class InterludeDefinition
    {
        public InterludeDefinition(bool isPreFinale, params InterludeLine[] lines)
        {
            IsPreFinale = isPreFinale;
            Lines = lines;
        }

        public InterludeLine[] Lines { get; }

        /// <summary>"Prima del liv. 7" entries: data ready, NOT hooked until level 7 exists.</summary>
        public bool IsPreFinale { get; }
    }

    /// <summary>
    /// All interlude texts from Docs/10_Narrative_Spine.md §5, VERBATIM (approved game text —
    /// never rewrite here). Keyed by (campaign, level just completed). Entries for levels that
    /// do not exist yet are already in place and hook up automatically as levels are added.
    /// </summary>
    public static class InterludeCatalog
    {
        /// <summary>The interlude to show after completing <paramref name="completedLevel"/>,
        /// or null (no beat / pre-finale entries are excluded until level 7 exists).</summary>
        public static InterludeDefinition? AfterLevel(Campaign campaign, int completedLevel)
        {
            Dictionary<int, InterludeDefinition> table = campaign == Campaign.ViaOscura ? Oscura : Romana;
            if (!table.TryGetValue(completedLevel, out InterludeDefinition? def))
                return null;
            return def.IsPreFinale ? null : def;
        }

        // ---- Via Oscura — ricordi che sbiadiscono (la voce cresce) ----
        private static readonly Dictionary<int, InterludeDefinition> Oscura = new Dictionary<int, InterludeDefinition>
        {
            [1] = new InterludeDefinition(false,
                new InterludeLine("Ricordo il campo prima della battaglia. Marcus affilava la spada e rideva"),
                new InterludeLine("di una cosa che ho detto. Ricordo la sua risata."),
                new InterludeLine("Una voce, sotto: «Non ti servirà.»", isVoice: true)),
            [2] = new InterludeDefinition(false,
                new InterludeLine("Ricordo il campo prima della battaglia. Qualcuno affilava una spada."),
                new InterludeLine("Rideva. Non ricordo di cosa."),
                new InterludeLine("«Il sangue ricorda per te.»", isVoice: true)),
            [3] = new InterludeDefinition(false,
                new InterludeLine("C'era un campo. C'era una risata. Il fuoco di stanotte era più caldo"),
                new InterludeLine("di quel ricordo."),
                new InterludeLine("«Brucia anche il resto.»", isVoice: true)),
            [4] = new InterludeDefinition(false,
                new InterludeLine("Ho giurato qualcosa, una volta. A un'aquila, a un uomo, a una città."),
                new InterludeLine("Le parole non tornano."),
                new InterludeLine("«Hai giurato a me. All'inizio dei tempi.»", isVoice: true)),
            [5] = new InterludeDefinition(false,
                new InterludeLine("La sciamana mi ha guardato dentro, prima di cadere. Ha detto un nome."),
                new InterludeLine("Non era il mio. O forse sì."),
                new InterludeLine("«Il tuo nome è quello che scrivo io.»", isVoice: true)),
            [6] = new InterludeDefinition(false,
                new InterludeLine("Roma. Conosco queste strade. Non ricordo perché."),
                new InterludeLine("«Le conosci perché stanno per essere mie.»", isVoice: true)),
            [7] = new InterludeDefinition(true,   // "Prima del liv. 7 (Il Trono)" — data point, unhooked
                new InterludeLine("L'uomo sul trono mi chiama per nome. Lucius."),
                new InterludeLine("Lucius. Lucius."),
                new InterludeLine("Non significa niente.")),
        };

        // ---- Via Romana — il peso del Favore ----
        private static readonly Dictionary<int, InterludeDefinition> Romana = new Dictionary<int, InterludeDefinition>
        {
            [1] = new InterludeDefinition(false,
                new InterludeLine("La freccia Nothung è leggera come un giunco. Allora perché la mia mano"),
                new InterludeLine("trema nel reggerla?"),
                new InterludeLine("Sei guerrieri hanno risposto alla chiamata. Ho promesso di riportarli tutti.")),
            [2] = new InterludeDefinition(false,
                new InterludeLine("Orako resta al Passo. «Qualcuno deve tenere aperta la via del ritorno»,"),
                new InterludeLine("ha detto. Non ha detto: <i>per chi tornerà</i>.")),
            [3] = new InterludeDefinition(false,
                new InterludeLine("Gaull ha voluto restare nella sua terra, tra le sue tribù in lutto."),
                new InterludeLine("Gli ho dato l'ordine che mi ha chiesto. Un imperatore non dice addio.")),
            [4] = new InterludeDefinition(false,
                new InterludeLine("Ferox presidia il porto e ne è felice: «Un molo è solo un'arena più lunga.»"),
                new InterludeLine("Ho riso. Marduk no. Il Favore non conosce la leggerezza.")),
            [5] = new InterludeDefinition(false,
                new InterludeLine("Naja è rimasta all'oasi con le sue Haife, a guarire l'acqua."),
                new InterludeLine("Seronneth dice che gli spiriti del deserto sono inquieti. Le credo."),
                new InterludeLine("Restano lei e Hilyus. Poi sarò solo.")),
            [6] = new InterludeDefinition(false,
                new InterludeLine("Il generale deforme si è battuto meglio di chiunque io abbia mai"),
                new InterludeLine("affrontato. Morendo, sorrideva. «Lei ti aspetta dove tutto è iniziato.»"),
                new InterludeLine("Seronneth e Hilyus custodiscono le rovine alle mie spalle."),
                new InterludeLine("Davanti a me, solo il tempio. Solo io. Così vuole il dio.")),
            [7] = new InterludeDefinition(true,   // "Prima del liv. 7 (Il Tempio)" — data point, unhooked
                new InterludeLine("Marduk tace. La freccia pesa, adesso."),
                new InterludeLine("Dall'oscurità, una voce che conosco: «Imperatore.»"),
                new InterludeLine("È la voce di Lucius. Non è la voce di Lucius.")),
        };
    }
}
