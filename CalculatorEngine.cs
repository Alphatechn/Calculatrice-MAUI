using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Calculatrice;

public class CalculatorEngine
{
    private const int MaxChiffres = 15;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private string _saisie = "0";          // nombre en cours de saisie
    private decimal? _accumulateur;        // premier opérande
    private string? _operateur;            // "+", "−", "×", "÷"
    private bool _nouvelleSaisie;          // le prochain chiffre remplace l'affichage
    private bool _attenteOperande;         // opérateur choisi, second nombre pas encore saisi
    private bool _resultatAffiche;         // un résultat final est affiché
    private string? _dernierOperateur;     // pour répéter "="
    private decimal _dernierOperande;

    public string Affichage { get; private set; } = "0";
    public string Expression { get; private set; } = "";
    public bool EnErreur { get; private set; }

    /// <summary>Déclenché à chaque calcul terminé (sert à l'historique).</summary>
    public event Action<string>? CalculTermine;

    private decimal ValeurCourante => decimal.Parse(_saisie, NumberStyles.Number, Inv);

    // ================= SAISIE =================

    public void SaisirChiffre(string chiffre)
    {
        if (EnErreur) ToutEffacer();
        PreparerNouvelleSaisie();
        if (_saisie.Count(char.IsDigit) >= MaxChiffres) return; // évite le débordement

        _saisie = _saisie switch
        {
            "0" => chiffre,
            "-0" => "-" + chiffre,
            _ => _saisie + chiffre
        };
        _attenteOperande = false;
        MettreAJour();
    }

    public void SaisirVirgule()
    {
        if (EnErreur) ToutEffacer();
        PreparerNouvelleSaisie();
        if (!_saisie.Contains('.')) _saisie += ".";
        _attenteOperande = false;
        MettreAJour();
    }

    public void EffacerDernier()
    {
        if (EnErreur) { ToutEffacer(); return; }
        if (_nouvelleSaisie) return; // on n'efface pas un résultat caractère par caractère

        _saisie = _saisie.Length > 1 ? _saisie[..^1] : "0";
        if (_saisie is "-" or "" or "-0") _saisie = "0";
        MettreAJour();
    }

    public void ToutEffacer()
    {
        _saisie = "0";
        _accumulateur = null;
        _operateur = null;
        _nouvelleSaisie = false;
        _attenteOperande = false;
        _resultatAffiche = false;
        _dernierOperateur = null;
        EnErreur = false;
        Expression = "";
        MettreAJour();
    }

    public void ChangerSigne()
    {
        if (EnErreur || ValeurCourante == 0) return;
        _saisie = _saisie.StartsWith('-') ? _saisie[1..] : "-" + _saisie;
        _attenteOperande = false;
        MettreAJour();
    }

    public void Pourcentage()
    {
        if (EnErreur) return;
        decimal v = ValeurCourante;

        // 200 + 10 %  →  200 + 20  (comme sur les calculatrices de poche)
        // 50 %        →  0,5
        decimal r = (_accumulateur.HasValue && (_operateur is "+" or "−"))
            ? _accumulateur.Value * v / 100
            : v / 100;

        DefinirSaisie(r);
        _nouvelleSaisie = true;
        _attenteOperande = false;
        MettreAJour();
    }

    // ================= OPÉRATIONS =================

    public void ChoisirOperateur(string op)
    {
        if (EnErreur) return;

        if (_operateur != null && !_attenteOperande)
        {
            // Calcul enchaîné : 2 + 3 ×  →  5 ×
            decimal? r = Calculer(_accumulateur!.Value, _operateur, ValeurCourante);
            if (r is null) return;
            _accumulateur = r;
            DefinirSaisie(r.Value);
        }
        else if (_operateur == null)
        {
            _accumulateur = ValeurCourante;
        }
        // Sinon, l'utilisateur change simplement d'opérateur (5 + puis ×)

        _operateur = op;
        _attenteOperande = true;
        _nouvelleSaisie = true;
        _resultatAffiche = false;
        Expression = $"{Formater(_accumulateur!.Value)} {op}";
        MettreAJour();
    }

    public void Egal()
    {
        if (EnErreur) return;

        decimal a, b;
        string op;

        if (_operateur != null)
        {
            a = _accumulateur!.Value; b = ValeurCourante; op = _operateur;
        }
        else if (_dernierOperateur != null)
        {
            // "=" répété : 5 + 3 = = =  →  8, 11, 14
            a = ValeurCourante; b = _dernierOperande; op = _dernierOperateur;
        }
        else return;

        Expression = $"{Formater(a)} {op} {Formater(b)} =";
        decimal? r = Calculer(a, op, b);
        if (r is null) return;

        _dernierOperateur = op;
        _dernierOperande = b;
        _operateur = null;
        _accumulateur = null;
        _attenteOperande = false;
        DefinirSaisie(r.Value);
        _nouvelleSaisie = true;
        _resultatAffiche = true;
        MettreAJour();

        CalculTermine?.Invoke($"{Expression} {Affichage}");
    }

    /// <summary>Fonctions avancées (bonus) : √, x², 1/x.</summary>
    public void AppliquerFonction(string fonction)
    {
        if (EnErreur) return;
        decimal v = ValeurCourante;

        string libelle = fonction switch
        {
            "√" => $"√({Formater(v)})",
            "x²" => $"({Formater(v)})²",
            "1/x" => $"1/({Formater(v)})",
            _ => fonction
        };
        string expr = _operateur != null
            ? $"{Formater(_accumulateur!.Value)} {_operateur} {libelle}"
            : $"{libelle} =";
        Expression = expr;

        decimal? r;
        try
        {
            r = fonction switch
            {
                "√" when v < 0 => Erreur("Racine d'un nombre négatif"),
                "√" => (decimal)Math.Sqrt((double)v),
                "x²" => v * v,
                "1/x" when v == 0 => Erreur("Division par zéro impossible"),
                "1/x" => 1 / v,
                _ => null
            };
        }
        catch (OverflowException)
        {
            r = Erreur("Nombre trop grand");
        }
        if (r is null) return;

        bool calculFinal = _operateur == null;
        DefinirSaisie(r.Value);
        _attenteOperande = false;
        _nouvelleSaisie = true;
        _resultatAffiche = calculFinal;
        MettreAJour();

        if (calculFinal) CalculTermine?.Invoke($"{expr} {Affichage}");
    }

    // ================= OUTILS INTERNES =================

    private decimal? Calculer(decimal a, string op, decimal b)
    {
        try
        {
            if (op == "÷" && b == 0) return Erreur("Division par zéro impossible");

            return op switch
            {
                "+" => a + b,
                "−" => a - b,
                "×" => a * b,
                "÷" => a / b,
                _ => throw new InvalidOperationException($"Opérateur inconnu : {op}")
            };
        }
        catch (OverflowException)
        {
            return Erreur("Nombre trop grand");
        }
    }

    private decimal? Erreur(string message)
    {
        EnErreur = true;
        Affichage = message;
        _saisie = "0";
        _accumulateur = null;
        _operateur = null;
        _dernierOperateur = null;
        _attenteOperande = false;
        _nouvelleSaisie = true;
        _resultatAffiche = true;
        return null;
    }

    private void PreparerNouvelleSaisie()
    {
        if (!_nouvelleSaisie) return;
        _saisie = "0";
        _nouvelleSaisie = false;
        if (_resultatAffiche)
        {
            Expression = "";           // un nouveau calcul commence
            _resultatAffiche = false;
        }
    }

    private void DefinirSaisie(decimal v)
    {
        v = Math.Round(v, 10);                       // 10 décimales maximum
        _saisie = v == 0 ? "0" : v.ToString("0.##########", Inv);
    }

    private void MettreAJour()
    {
        if (!EnErreur) Affichage = Joli(_saisie);
    }

    private static string Formater(decimal v)
    {
        v = Math.Round(v, 10);
        return Joli(v == 0 ? "0" : v.ToString("0.##########", Inv));
    }

    /// <summary>"-1234567.5" devient "-1 234 567,5"</summary>
    private static string Joli(string saisie)
    {
        bool negatif = saisie.StartsWith('-');
        string corps = negatif ? saisie[1..] : saisie;
        int point = corps.IndexOf('.');
        string entier = point >= 0 ? corps[..point] : corps;
        string decimales = point >= 0 ? "," + corps[(point + 1)..] : "";

        var sb = new StringBuilder();
        for (int i = 0; i < entier.Length; i++)
        {
            if (i > 0 && (entier.Length - i) % 3 == 0) sb.Append(' ');
            sb.Append(entier[i]);
        }
        return (negatif ? "-" : "") + sb + decimales;
    }
}