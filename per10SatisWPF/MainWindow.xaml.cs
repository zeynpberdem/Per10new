using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using per10SatisWPF.Models;

namespace per10SatisWPF
{
    public partial class MainWindow : Window
    {
        private readonly string _connStr = @"Data Source=MertPC\SQLEXPRESS;Initial Catalog=per10Database;User ID=sa;Password=1;Encrypt=True;TrustServerCertificate=True";

        private List<SepetItem> _sepet = new();
        private List<Urun> _urunler = new();
        private int _aktifTurID = 0;
        private string _numpadBuffer = "";

        private readonly List<(int TurID, string Ad)> _kategoriler = new()
        {
            (0, "Tümü"),
            (1, "Gıda"),
            (2, "Atıştırmalık"),
            (3, "İçecek"),
            (4, "Kişisel Bakım"),
            (5, "Temizlik"),
            (6, "Ev & Yaşam"),
            (7, "Elektronik"),
            (8, "Giyim"),
            (9, "Diğer"),
        };

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                KategorileriYukle();
                UrunleriYukle(0);
                PlaceholderSet(txtBarkod);
                PlaceholderSet(txtArama);
            };
        }

        // ─── KATEGORİLER ──────────────────────────────────────────────
        private void KategorileriYukle()
        {
            pnlKategoriler.Children.Clear();
            foreach (var (turID, ad) in _kategoriler)
            {
                bool aktif = turID == _aktifTurID;
                var btn = new Button
                {
                    Content = ad,
                    Tag = turID,
                    Height = 38,
                    Margin = new Thickness(0, 2, 0, 2),
                    FontSize = 13,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(12, 0, 0, 0),
                    Background = aktif ? (Brush)FindResource("AccentBlue") : (Brush)FindResource("BgCard"),
                    Foreground = Brushes.White,
                    Template = (ControlTemplate)FindResource("RoundBtnTemplate")
                };
                btn.Click += KategoriBtn_Click;
                pnlKategoriler.Children.Add(btn);
            }
        }

        private void KategoriBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int turID)
            {
                _aktifTurID = turID;
                KategorileriYukle();
                UrunleriYukle(turID);
            }
        }

        // ─── ÜRÜNLER ──────────────────────────────────────────────────
        private void UrunleriYukle(int turID, string? arama = null)
        {
            _urunler.Clear();
            pnlUrunler.Children.Clear();

            string query = @"SELECT u.UrunID, u.UrunAdi, m.MarkaAdi, u.SatisFiyati, u.MevcutStok, u.TurID
                             FROM Urunler u
                             JOIN Markalar m ON u.MarkaID = m.MarkaID
                             WHERE u.MevcutStok > 0";

            if (turID > 0) query += " AND u.TurID = @turID";
            if (!string.IsNullOrWhiteSpace(arama))
                query += " AND (u.UrunAdi LIKE @arama OR m.MarkaAdi LIKE @arama)";

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand(query, conn);
                if (turID > 0) cmd.Parameters.AddWithValue("@turID", turID);
                if (!string.IsNullOrWhiteSpace(arama)) cmd.Parameters.AddWithValue("@arama", $"%{arama}%");

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var urun = new Urun
                    {
                        UrunID      = Convert.ToInt32(reader["UrunID"]),
                        UrunAdi     = reader["UrunAdi"].ToString()!,
                        MarkaAdi    = reader["MarkaAdi"].ToString()!,
                        SatisFiyati = Convert.ToDecimal(reader["SatisFiyati"]),
                        MevcutStok  = Convert.ToInt32(reader["MevcutStok"]),
                        TurID       = Convert.ToInt32(reader["TurID"])
                    };
                    _urunler.Add(urun);
                    pnlUrunler.Children.Add(UrunKartiOlustur(urun));
                }
            }
            catch (Exception ex) { MessageBox.Show($"Ürün yükleme hatası: {ex.Message}"); }
        }

        private Border UrunKartiOlustur(Urun urun)
        {
            var stokRenk = urun.DusukStok
                ? (Brush)FindResource("AccentOrange")
                : (Brush)FindResource("AccentGreen");

            var border = new Border
            {
                Width = 158, Height = 128,
                Margin = new Thickness(6),
                CornerRadius = new CornerRadius(10),
                Background = (Brush)FindResource("BgCard"),
                BorderBrush = (Brush)FindResource("BorderColor"),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Tag = urun
            };

            var grid = new Grid { Margin = new Thickness(12) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var ad    = new TextBlock { Text = urun.TamAdi, Foreground = (Brush)FindResource("TextWhite"), FontSize = 12, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            var fiyat = new TextBlock { Text = urun.FiyatText, Foreground = (Brush)FindResource("AccentBlue"), FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 0) };
            var stok  = new TextBlock { Text = urun.StokText, Foreground = stokRenk, FontSize = 11, Margin = new Thickness(0, 2, 0, 0) };

            Grid.SetRow(fiyat, 1);
            Grid.SetRow(stok, 2);
            grid.Children.Add(ad);
            grid.Children.Add(fiyat);
            grid.Children.Add(stok);
            border.Child = grid;

            border.MouseDown  += (s, e) => SepeteEkle(urun);
            border.MouseEnter += (s, e) => border.Background = new SolidColorBrush(Color.FromRgb(0x2F, 0x36, 0x50));
            border.MouseLeave += (s, e) => border.Background = (Brush)FindResource("BgCard");

            return border;
        }

        // ─── SEPET ────────────────────────────────────────────────────
        private void SepeteEkle(Urun urun)
        {
            var mevcut = _sepet.FirstOrDefault(x => x.UrunID == urun.UrunID);
            if (mevcut != null)
            {
                int stok = StokGetir(urun.UrunID);
                if (mevcut.Adet < stok) { mevcut.Adet++; SepetYenile(); }
                else MessageBox.Show($"Stok yetersiz! Maksimum: {stok}", "Uyarı");
                return;
            }
            _sepet.Add(new SepetItem { UrunID = urun.UrunID, UrunAdi = urun.TamAdi, BirimFiyat = urun.SatisFiyati, Adet = 1 });
            SepetYenile();
        }

        private int StokGetir(int urunID)
        {
            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand("SELECT MevcutStok FROM Urunler WHERE UrunID = @id", conn);
                cmd.Parameters.AddWithValue("@id", urunID);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch { return 0; }
        }

        private void SepetYenile()
        {
            pnlSepet.Children.Clear();
            foreach (var item in _sepet)
                pnlSepet.Children.Add(SepetSatiriOlustur(item));
            lblToplam.Text = $"{_sepet.Sum(x => x.ToplamFiyat):N2} ₺";
        }

        private Border SepetSatiriOlustur(SepetItem item)
        {
            var border = new Border
            {
                Background = (Brush)FindResource("BgCard"),
                CornerRadius = new CornerRadius(8),
                BorderBrush = (Brush)FindResource("BorderColor"),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 3, 0, 3),
                Padding = new Thickness(10, 8, 10, 8)
            };

            var root = new StackPanel();

            // Üst satır: ad + sil
            var ustGrid = new Grid();
            var txtAd = new TextBlock
            {
                Text = item.UrunAdi,
                Foreground = (Brush)FindResource("TextWhite"),
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap, MaxWidth = 220
            };
            var btnSil = new Button
            {
                Content = "✕",
                Background = Brushes.Transparent,
                Foreground = (Brush)FindResource("AccentRed"),
                BorderThickness = new Thickness(0),
                FontSize = 13, Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                Tag = item.UrunID
            };
            btnSil.Click += (s, e) =>
            {
                _sepet.RemoveAll(x => x.UrunID == item.UrunID);
                SepetYenile();
            };
            ustGrid.Children.Add(txtAd);
            ustGrid.Children.Add(btnSil);

            // Alt satır: adet +/- + toplam fiyat
            var altGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            var sol = new StackPanel { Orientation = Orientation.Horizontal };

            var btnAzalt = AdetBtn("−", (Brush)FindResource("NumpadBg"));
            var txtAdet  = new TextBlock { Text = item.Adet.ToString(), Foreground = (Brush)FindResource("TextWhite"), FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0), MinWidth = 20, TextAlignment = TextAlignment.Center };
            var btnArtir = AdetBtn("+", (Brush)FindResource("AccentBlue"));

            btnAzalt.Click += (s, e) =>
            {
                if (item.Adet > 1) { item.Adet--; SepetYenile(); }
                else MessageBox.Show("Silmek için ✕ kullanın.", "Uyarı");
            };
            btnArtir.Click += (s, e) =>
            {
                int stok = StokGetir(item.UrunID);
                if (item.Adet < stok) { item.Adet++; SepetYenile(); }
                else MessageBox.Show($"Stok yetersiz! Maks: {stok}", "Uyarı");
            };

            sol.Children.Add(btnAzalt);
            sol.Children.Add(txtAdet);
            sol.Children.Add(btnArtir);

            var txtToplam = new TextBlock
            {
                Text = item.ToplamFiyatText,
                Foreground = (Brush)FindResource("AccentBlue"),
                FontSize = 13, FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            altGrid.Children.Add(sol);
            altGrid.Children.Add(txtToplam);

            root.Children.Add(ustGrid);
            root.Children.Add(altGrid);
            border.Child = root;
            return border;
        }

        private Button AdetBtn(string icerik, Brush bg) => new Button
        {
            Content = icerik, Width = 26, Height = 26, FontSize = 14,
            Background = bg, Foreground = Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Template = (ControlTemplate)FindResource("SmallRoundBtnTemplate")
        };

        private void btnSepetTemizle_Click(object sender, RoutedEventArgs e)
        {
            if (_sepet.Count == 0) return;
            if (MessageBox.Show("Sepeti temizlemek istediğinize emin misiniz?", "Onay", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            { _sepet.Clear(); SepetYenile(); }
        }

        // ─── BARKOD ───────────────────────────────────────────────────
        private void txtBarkod_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            string barkod = txtBarkod.Text.Trim();
            if (barkod == (string)txtBarkod.Tag || string.IsNullOrEmpty(barkod)) return;

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand(
                    @"SELECT u.UrunID, u.UrunAdi, m.MarkaAdi, u.SatisFiyati, u.MevcutStok, u.TurID
                      FROM Urunler u JOIN Markalar m ON u.MarkaID = m.MarkaID
                      WHERE u.Barkod = @barkod AND u.MevcutStok > 0", conn);
                cmd.Parameters.AddWithValue("@barkod", barkod);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                    SepeteEkle(new Urun { UrunID = Convert.ToInt32(r["UrunID"]), UrunAdi = r["UrunAdi"].ToString()!, MarkaAdi = r["MarkaAdi"].ToString()!, SatisFiyati = Convert.ToDecimal(r["SatisFiyati"]), MevcutStok = Convert.ToInt32(r["MevcutStok"]) });
                else
                    MessageBox.Show($"'{barkod}' barkoduna ait ürün bulunamadı!", "Uyarı");
            }
            catch (Exception ex) { MessageBox.Show($"Barkod hatası: {ex.Message}"); }

            txtBarkod.Clear();
        }

        // ─── NUMPAD ───────────────────────────────────────────────────
        private void Numpad_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn) { _numpadBuffer += btn.Content; lblNumpad.Text = _numpadBuffer; }
        }

        private void NumpadC_Click(object sender, RoutedEventArgs e)
        { _numpadBuffer = ""; lblNumpad.Text = ""; }

        // ─── ÖDEME ────────────────────────────────────────────────────
        private void btnNakit_Click(object sender, RoutedEventArgs e) => OdemeYap("Nakit");
        private void btnKredi_Click(object sender, RoutedEventArgs e) => OdemeYap("KrediKarti");

        private void OdemeYap(string yontem)
        {
            if (_sepet.Count == 0) { MessageBox.Show("Sepet boş!", "Uyarı"); return; }
            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();

                var cmdSepet = new SqlCommand("INSERT INTO Sepetler (Tarih) VALUES (GETDATE()); SELECT SCOPE_IDENTITY();", conn);
                int sepetID = Convert.ToInt32(cmdSepet.ExecuteScalar());

                foreach (var item in _sepet)
                {
                    using var cmd = new SqlCommand(
                        "INSERT INTO Satislar (UrunID, Miktar, birimalisfiyati, birimsatisfiyati, SatisTarihi, SepetID) VALUES (@id,@miktar,@alis,@satis,GETDATE(),@sepetId)", conn);
                    cmd.Parameters.AddWithValue("@id",      item.UrunID);
                    cmd.Parameters.AddWithValue("@miktar",  item.Adet);
                    cmd.Parameters.AddWithValue("@alis",    20m);
                    cmd.Parameters.AddWithValue("@satis",   item.BirimFiyat);
                    cmd.Parameters.AddWithValue("@sepetId", sepetID);
                    cmd.ExecuteNonQuery();
                }

                decimal toplam = _sepet.Sum(x => x.ToplamFiyat);
                MessageBox.Show($"✅ Ödeme Başarılı!\nYöntem: {yontem}\nToplam: {toplam:N2} ₺", "Başarılı");
                _sepet.Clear();
                _numpadBuffer = "";
                lblNumpad.Text = "";
                SepetYenile();
                UrunleriYukle(_aktifTurID);
            }
            catch (Exception ex) { MessageBox.Show($"Ödeme hatası: {ex.Message}"); }
        }

        // ─── ARAMA ────────────────────────────────────────────────────
        private void txtArama_TextChanged(object sender, TextChangedEventArgs e)
        {
            string metin = txtArama.Text;
            if (metin == (string)txtArama.Tag) return;
            UrunleriYukle(_aktifTurID, metin);
        }

        // ─── SON SATIŞ İPTAL ──────────────────────────────────────────
        private void btnSatisgeri_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Son satışı iptal etmek istediğinize emin misiniz?", "Onay", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                var cmd = new SqlCommand("SELECT MAX(SepetID) FROM Sepetler", conn);
                object res = cmd.ExecuteScalar();
                if (res != DBNull.Value)
                {
                    var sp = new SqlCommand("sp_SepetIptal", conn) { CommandType = CommandType.StoredProcedure };
                    sp.Parameters.AddWithValue("@SepetID", Convert.ToInt32(res));
                    sp.ExecuteNonQuery();
                    MessageBox.Show("Son satış iptal edildi, stoklar geri yüklendi!", "Başarılı");
                    UrunleriYukle(_aktifTurID);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        // ─── AYARLAR ──────────────────────────────────────────────────
        private void btnAyarlar_Click(object sender, RoutedEventArgs e)
        {
            new AyarlarWindow().ShowDialog();
        }

        // ─── KLAVYE KISAYOLLARI ───────────────────────────────────────
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F1) OdemeYap("Nakit");
            else if (e.Key == Key.F4) OdemeYap("KrediKarti");
        }

        // ─── PLACEHOLDER ──────────────────────────────────────────────
        private void PlaceholderSet(TextBox tb)
        {
            tb.Text = tb.Tag?.ToString() ?? "";
            tb.Foreground = (Brush)FindResource("TextGray");
        }

        private void Placeholder_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.Text == tb.Tag?.ToString())
            { tb.Text = ""; tb.Foreground = (Brush)FindResource("TextWhite"); }
        }

        private void Placeholder_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && string.IsNullOrEmpty(tb.Text))
                PlaceholderSet(tb);
        }

        // ─── KAPAT ────────────────────────────────────────────────────
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (MessageBox.Show("Programı kapatmak istiyor musunuz?", "Çıkış", MessageBoxButton.YesNo) == MessageBoxResult.No)
                e.Cancel = true;
        }
    }
}
