namespace Calculatrice;

public partial class MainPage : ContentPage
{
    private readonly CalculatorEngine _calc = new();
    private double _largeur = -1, _hauteur = -1;
    private double _policeMax = 56;

    public MainPage()
    {
        InitializeComponent();
        _calc.CalculTermine += AjouterHistorique;
        EcranBorder.SizeChanged += (_, _) => AjusterPolice();
        RafraichirAffichage();
    }

    // ================= ÉVÉNEMENTS DU CLAVIER =================

    private void OnChiffreClicked(object? sender, EventArgs e)
    {
        if (sender is Button b) Executer(() => _calc.SaisirChiffre(b.Text));
    }

    private void OnOperateurClicked(object? sender, EventArgs e)
    {
        if (sender is Button b) Executer(() => _calc.ChoisirOperateur(b.Text));
    }

    private void OnFonctionClicked(object? sender, EventArgs e)
    {
        if (sender is Button b) Executer(() => _calc.AppliquerFonction(b.Text));
    }

    private void OnVirguleClicked(object? sender, EventArgs e) => Executer(_calc.SaisirVirgule);
    private void OnEgalClicked(object? sender, EventArgs e) => Executer(_calc.Egal);
    private void OnEffacerToutClicked(object? sender, EventArgs e) => Executer(_calc.ToutEffacer);
    private void OnEffacerDernierClicked(object? sender, EventArgs e) => Executer(_calc.EffacerDernier);
    private void OnSigneClicked(object? sender, EventArgs e) => Executer(_calc.ChangerSigne);
    private void OnPourcentageClicked(object? sender, EventArgs e) => Executer(_calc.Pourcentage);

    private void Executer(Action action)
    {
        action();
        RafraichirAffichage();
    }

    // ================= AFFICHAGE =================

    private void RafraichirAffichage()
    {
        ResultatLabel.Text = _calc.Affichage;
        ResultatLabel.TextColor = _calc.EnErreur ? Color.FromArgb("#FF453A") : Colors.White;
        ExpressionLabel.Text = string.IsNullOrEmpty(_calc.Expression) ? " " : _calc.Expression;
        AjusterPolice();

        // Toujours montrer la fin de l'opération en cours
        Dispatcher.Dispatch(async () =>
        {
            try { await ExpressionScroll.ScrollToAsync(ExpressionLabel, ScrollToPosition.End, false); }
            catch { /* mise en page pas encore prête : sans conséquence */ }
        });
    }

    /// <summary>Réduit la police du résultat pour qu'il tienne toujours dans l'écran.</summary>
    private void AjusterPolice()
    {
        double largeurDispo = EcranBorder.Width > 0 ? EcranBorder.Width - 32 : 300;
        int nbCaracteres = Math.Max(1, ResultatLabel.Text?.Length ?? 1);
        double taille = Math.Min(_policeMax, largeurDispo / (0.62 * nbCaracteres));
        ResultatLabel.FontSize = Math.Max(16, taille);
    }

    // ================= ORIENTATION ET TAILLE D'ÉCRAN =================

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0) return;
        if (Math.Abs(width - _largeur) < 1 && Math.Abs(height - _hauteur) < 1) return;

        _largeur = width;
        _hauteur = height;
        AppliquerDisposition();
    }

    private void AppliquerDisposition()
    {
        bool paysage = _largeur > _hauteur;
        RootGrid.RowDefinitions.Clear();
        RootGrid.ColumnDefinitions.Clear();

        if (paysage)
        {
            // Paysage : écran à gauche, clavier à droite
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.3, GridUnitType.Star)));
            Grid.SetRow(ZoneScroll, 0); Grid.SetColumn(ZoneScroll, 0);
            Grid.SetRow(Clavier, 0); Grid.SetColumn(Clavier, 1);
            ZoneScroll.VerticalOptions = LayoutOptions.Center;
            Clavier.RowSpacing = 6;
            _policeMax = 44;
        }
        else
        {
            // Portrait : écran en haut, clavier en bas avec des touches presque carrées
            double hauteurClavier = Math.Min(_hauteur * 0.6, (_largeur - 24) * 1.25);
            RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            RootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(hauteurClavier)));
            Grid.SetRow(ZoneScroll, 0); Grid.SetColumn(ZoneScroll, 0);
            Grid.SetRow(Clavier, 1); Grid.SetColumn(Clavier, 0);
            ZoneScroll.VerticalOptions = LayoutOptions.End;
            Clavier.RowSpacing = 10;
            _policeMax = 56;
        }

        double policeTouches = paysage
            ? Math.Clamp(_hauteur / 18, 16, 24)
            : Math.Clamp(_largeur / 14, 20, 30);

        foreach (var enfant in Clavier.Children)
            if (enfant is Button touche) touche.FontSize = policeTouches;

        RafraichirAffichage();
    }

    // ================= HISTORIQUE (BONUS) =================

    private void AjouterHistorique(string ligne)
    {
        HistoriqueStack.Children.Insert(0, new Label
        {
            Text = ligne,
            TextColor = Color.FromArgb("#9AA4AF"),
            FontSize = 14,
            HorizontalTextAlignment = TextAlignment.End
        });

        while (HistoriqueStack.Children.Count > 30)
            HistoriqueStack.Children.RemoveAt(HistoriqueStack.Children.Count - 1);
    }

    private void OnHistoriqueClicked(object? sender, EventArgs e)
    {
        HistoriqueBorder.IsVisible = !HistoriqueBorder.IsVisible;
        HistoriqueButton.Text = HistoriqueBorder.IsVisible ? "🕘 Masquer" : "🕘 Historique";
    }

    private void OnViderHistoriqueClicked(object? sender, EventArgs e)
        => HistoriqueStack.Children.Clear();
}