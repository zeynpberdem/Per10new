using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Data.SqlClient;

namespace per10SatisWPF
{
    public partial class AyarlarWindow : Window
    {
        private readonly string _connStr = @"Data Source=MertPC\SQLEXPRESS;Initial Catalog=per10Database;User ID=sa;Password=1;Encrypt=True;TrustServerCertificate=True";
        private int _seciliUrunID = -1;

        private readonly List<(int TurID, string Ad)> _kategoriler = new()
        {
            (1, "Motor Bakım"),
            (2, "Cam Temizleyiciler"),
            (3, "Parfümler"),
            (4, "Jant ve Lastik Bakımı"),
            (5, "Parlatma ve Koruma"),
            (6, "Bezler ve Süngerler"),
            (7, "İçecekler"),
            (8, "Araç İç Bakım"),
            (9, "Diğerleri"),
        };

        public AyarlarWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Tarih varsayılanları
            dpBaslangic.SelectedDate    = DateTime.Today.AddDays(-30);
            dpBitis.SelectedDate        = DateTime.Today;
            dpGrafBaslangic.SelectedDate = DateTime.Today.AddDays(-30);
            dpGrafBitis.SelectedDate    = DateTime.Today;

            // ComboBox doldur
            foreach (var (turID, ad) in _kategoriler)
            {
                cmbKategoriFiltre.Items.Add(new ComboBoxItem { Content = ad, Tag = turID });
                cmbYeniTur.Items.Add(new ComboBoxItem { Content = ad, Tag = turID });
            }
            cmbKategoriFiltre.SelectedIndex = 0;
            cmbYeniTur.SelectedIndex = 0;

            MarkalariYukle();
        }

        // ─── ÜRÜN YÖNETİMİ ────────────────────────────────────────────
        private void cmbKategoriFiltre_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (cmbKategoriFiltre.SelectedItem is ComboBoxItem item && item.Tag is int turID)
                UrunleriListele(turID);
        }

        private void UrunleriListele(int turID)
        {
            var tablo = new DataTable();
            try
            {
                using var conn = new SqlConnection(_connStr);
                string q = @"SELECT u.UrunID, u.UrunAdi, m.MarkaAdi,
                                    u.AlisFiyati, u.SatisFiyati, u.MevcutStok, u.Barkod
                             FROM Urunler u
                             JOIN Markalar m ON u.MarkaID = m.MarkaID
                             WHERE u.TurID = @turID
                             ORDER BY m.MarkaAdi, u.UrunAdi";
                new SqlDataAdapter(new SqlCommand(q, conn) { Parameters = { new SqlParameter("@turID", turID) } }, conn).Fill(tablo);
            }
            catch (Exception ex) { MessageBox.Show($"Hata: {ex.Message}"); return; }

            var liste = tablo.AsEnumerable().Select(r => new
            {
                UrunID          = r.Field<int>("UrunID"),
                TamAdi          = $"{r["MarkaAdi"]} {r["UrunAdi"]}",
                AlisFiyatiText  = $"{r.Field<decimal>("AlisFiyati"):N2} ₺",
                FiyatText       = $"{r.Field<decimal>("SatisFiyati"):N2} ₺",
                MevcutStok      = r.Field<int>("MevcutStok"),
                Barkod          = r["Barkod"]?.ToString() ?? ""
            }).ToList();

            dgUrunler.ItemsSource = liste;
        }

        private void dgUrunler_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgUrunler.SelectedItem == null) return;

            dynamic secili = dgUrunler.SelectedItem;
            _seciliUrunID = secili.UrunID;

            // Formu doldur
            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand(
                    "SELECT AlisFiyati, SatisFiyati, MevcutStok, Barkod FROM Urunler WHERE UrunID=@id", conn);
                cmd.Parameters.AddWithValue("@id", _seciliUrunID);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    txtAlisFiyati.Text     = r["AlisFiyati"].ToString();
                    txtSatisFiyati.Text    = r["SatisFiyati"].ToString();
                    txtStok.Text           = r["MevcutStok"].ToString();
                    txtBarkodGuncelle.Text = r["Barkod"]?.ToString() ?? "";
                }
            }
            catch { }
        }

        private void btnGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (_seciliUrunID < 0) { MessageBox.Show("Listeden bir ürün seçin.", "Uyarı"); return; }
            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand("sp_UrunGuncelleDetayli", conn)
                    { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@UrunID",     _seciliUrunID);
                cmd.Parameters.AddWithValue("@AlisFiyati", Convert.ToDecimal(txtAlisFiyati.Text));
                cmd.Parameters.AddWithValue("@SatisFiyati",Convert.ToDecimal(txtSatisFiyati.Text));
                cmd.Parameters.AddWithValue("@Stok",       Convert.ToInt32(txtStok.Text));
                cmd.ExecuteNonQuery();

                // Barkod güncelle
                if (!string.IsNullOrWhiteSpace(txtBarkodGuncelle.Text))
                {
                    using var cmdB = new SqlCommand("UPDATE Urunler SET Barkod=@b WHERE UrunID=@id", conn);
                    cmdB.Parameters.AddWithValue("@b",  txtBarkodGuncelle.Text.Trim());
                    cmdB.Parameters.AddWithValue("@id", _seciliUrunID);
                    cmdB.ExecuteNonQuery();
                }

                GosterMesaj(bdGuncelMesaj, lblGuncelMesaj, "✅ Ürün başarıyla güncellendi!");
                if (cmbKategoriFiltre.SelectedItem is ComboBoxItem item && item.Tag is int turID)
                    UrunleriListele(turID);
            }
            catch (Exception ex) { MessageBox.Show($"Güncelleme hatası: {ex.Message}"); }
        }

        // ─── YENİ ÜRÜN ────────────────────────────────────────────────
        private void MarkalariYukle()
        {
            try
            {
                using var conn = new SqlConnection(_connStr);
                var da = new SqlDataAdapter("SELECT MarkaID, MarkaAdi FROM Markalar ORDER BY MarkaAdi", conn);
                var dt = new DataTable();
                da.Fill(dt);
                cmbYeniMarka.DisplayMemberPath = "MarkaAdi";
                cmbYeniMarka.SelectedValuePath = "MarkaID";
                cmbYeniMarka.ItemsSource       = dt.DefaultView;
            }
            catch { }
        }

        private void btnYeniUrunEkle_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtYeniUrunAdi.Text) ||
                string.IsNullOrWhiteSpace(txtYeniAlis.Text) ||
                string.IsNullOrWhiteSpace(txtYeniSatis.Text) ||
                string.IsNullOrWhiteSpace(txtYeniStok.Text))
            {
                MessageBox.Show("Lütfen tüm zorunlu alanları doldurun.", "Eksik Bilgi");
                return;
            }

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand("sp_AkilliUrunEkle", conn)
                    { CommandType = CommandType.StoredProcedure };

                cmd.Parameters.AddWithValue("@YeniMarkaAdi",  DBNull.Value);
                cmd.Parameters.AddWithValue("@MevcutMarkaID", cmbYeniMarka.SelectedValue ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TurID",    (cmbYeniTur.SelectedItem as ComboBoxItem)?.Tag ?? 1);
                cmd.Parameters.AddWithValue("@UrunAdi",   txtYeniUrunAdi.Text.Trim());
                cmd.Parameters.AddWithValue("@AlisFiyati",  Convert.ToDecimal(txtYeniAlis.Text));
                cmd.Parameters.AddWithValue("@SatisFiyati", Convert.ToDecimal(txtYeniSatis.Text));
                cmd.Parameters.AddWithValue("@Stok",        Convert.ToInt32(txtYeniStok.Text));
                cmd.Parameters.AddWithValue("@KritikStok",  Convert.ToInt32(txtYeniKritikStok.Text));
                cmd.ExecuteNonQuery();

                // Barkod ekle
                if (!string.IsNullOrWhiteSpace(txtYeniBarkod.Text))
                {
                    using var cmdB = new SqlCommand(
                        "UPDATE Urunler SET Barkod=@b WHERE UrunAdi=@ad", conn);
                    cmdB.Parameters.AddWithValue("@b",  txtYeniBarkod.Text.Trim());
                    cmdB.Parameters.AddWithValue("@ad", txtYeniUrunAdi.Text.Trim());
                    cmdB.ExecuteNonQuery();
                }

                GosterMesaj(bdEkleMesaj, lblEkleMesaj,
                    $"✅ '{txtYeniUrunAdi.Text}' başarıyla eklendi!");

                txtYeniUrunAdi.Clear();
                txtYeniAlis.Clear();
                txtYeniSatis.Clear();
                txtYeniStok.Clear();
                txtYeniBarkod.Clear();
            }
            catch (Exception ex) { MessageBox.Show($"Kayıt hatası: {ex.Message}"); }
        }

        // ─── SATIŞ RAPORU ─────────────────────────────────────────────
        private void btnRaporGetir_Click(object sender, RoutedEventArgs e)
        {
            if (dpBaslangic.SelectedDate == null || dpBitis.SelectedDate == null) return;

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();

                // Özet
                using (var cmd = new SqlCommand("SatisAnalizGetir", conn)
                    { CommandType = CommandType.StoredProcedure })
                {
                    cmd.Parameters.AddWithValue("@baslangic", dpBaslangic.SelectedDate.Value.Date);
                    cmd.Parameters.AddWithValue("@bitis",     dpBitis.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1));
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        decimal ciro = Convert.ToDecimal(r["ToplamCiro"]);
                        decimal kar  = Convert.ToDecimal(r["ToplamKar"]);
                        lblCiro.Text  = $"{ciro:N2} ₺";
                        lblKar.Text   = $"{kar:N2} ₺";
                        lblKar.Foreground = kar >= 0
                            ? (Brush)FindResource("AccentGreen")
                            : (Brush)FindResource("AccentRed");
                    }
                }

                // Sepet listesi
                var dt = new DataTable();
                using (var cmd2 = new SqlCommand("sp_SepetleriFiltrele", conn)
                    { CommandType = CommandType.StoredProcedure })
                {
                    cmd2.Parameters.AddWithValue("@Baslangic", dpBaslangic.SelectedDate.Value.Date.AddHours(6));
                    cmd2.Parameters.AddWithValue("@Bitis",     dpBitis.SelectedDate.Value.Date.AddDays(1).AddHours(3));
                    new SqlDataAdapter(cmd2).Fill(dt);
                }

                var liste = dt.AsEnumerable().Select(r => new
                {
                    SepetID = r.Field<int>("SepetID"),
                    Toplam  = $"{r.Field<decimal>("ToplamTutar"):N2} ₺",
                    Tarih   = r.Field<DateTime>("Tarih").ToString("dd.MM.yyyy HH:mm")
                }).ToList();

                dgSepetler.ItemsSource = liste;
                lblIslem.Text = $"{liste.Count} işlem";
            }
            catch (Exception ex) { MessageBox.Show($"Rapor hatası: {ex.Message}"); }
        }

        private void dgSepetler_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgSepetler.SelectedItem == null) return;
            dynamic secili = dgSepetler.SelectedItem;

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand("sp_SepetDetayiniGetir", conn)
                    { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@SepetID", secili.SepetID);
                var dt = new DataTable();
                new SqlDataAdapter(cmd).Fill(dt);

                dgSepetDetay.ItemsSource = dt.AsEnumerable().Select(r => new
                {
                    UrunAdi = $"{r["MarkaAdi"]} {r["UrunAdi"]}",
                    Miktar  = r["Miktar"].ToString(),
                    Fiyat   = $"{Convert.ToDecimal(r["birimsatisfiyati"]):N2} ₺"
                }).ToList();
            }
            catch { }
        }

        // ─── GRAFİKLER ────────────────────────────────────────────────
        private void btnEnCokSatan_Click(object sender, RoutedEventArgs e) =>
            GrafikCiz("ilk5getir_procedure", "Adet", "#4A7CF6");

        private void btnEnCokKar_Click(object sender, RoutedEventArgs e) =>
            GrafikCiz("EnCokKarGetirenler", "ToplamKar", "#27AE60");

        private void GrafikCiz(string procedure, string degerKolonu, string renk)
        {
            if (dpGrafBaslangic.SelectedDate == null || dpGrafBitis.SelectedDate == null) return;

            cnvGrafik.Children.Clear();
            var veriler = new List<(string Ad, double Deger)>();

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand(procedure, conn)
                    { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@basTarihi", dpGrafBaslangic.SelectedDate.Value.Date);
                cmd.Parameters.AddWithValue("@bitTarihi", dpGrafBitis.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1));
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    veriler.Add((r[0].ToString()!, Convert.ToDouble(r[degerKolonu])));
            }
            catch (Exception ex) { MessageBox.Show($"Grafik hatası: {ex.Message}"); return; }

            if (veriler.Count == 0) { lblGrafikBos.Visibility = Visibility.Visible; return; }

            lblGrafikBos.Visibility = Visibility.Collapsed;
            svGrafik.Visibility     = Visibility.Visible;

            double maxDeger  = veriler.Max(v => v.Deger);
            double barGenislik = 60;
            double aralik     = 30;
            double maxYukseklik = 260;
            double toplam = veriler.Count * (barGenislik + aralik);

            cnvGrafik.Width = Math.Max(toplam + aralik, 600);

            for (int i = 0; i < veriler.Count; i++)
            {
                double x      = aralik + i * (barGenislik + aralik);
                double oran   = maxDeger > 0 ? veriler[i].Deger / maxDeger : 0;
                double yuksek = oran * maxYukseklik;
                double y      = maxYukseklik - yuksek + 20;

                // Çubuk
                var bar = new Rectangle
                {
                    Width  = barGenislik,
                    Height = yuksek,
                    Fill   = new SolidColorBrush((Color)ColorConverter.ConvertFromString(renk)!),
                    RadiusX = 6, RadiusY = 6, Opacity = 0.85
                };
                Canvas.SetLeft(bar, x);
                Canvas.SetTop(bar, y);
                cnvGrafik.Children.Add(bar);

                // Değer etiketi
                var txtDeger = new TextBlock
                {
                    Text       = degerKolonu == "Adet"
                        ? veriler[i].Deger.ToString("N0")
                        : $"{veriler[i].Deger:N0}₺",
                    Foreground = Brushes.White,
                    FontSize   = 11, FontWeight = FontWeights.Bold,
                    Width      = barGenislik, TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(txtDeger, x);
                Canvas.SetTop(txtDeger,  y - 22);
                cnvGrafik.Children.Add(txtDeger);

                // Ürün adı
                var txtAd = new TextBlock
                {
                    Text       = veriler[i].Ad.Length > 12
                        ? veriler[i].Ad.Substring(0, 12) + "..."
                        : veriler[i].Ad,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x7A, 0x99)),
                    FontSize   = 10, Width = barGenislik + aralik,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping  = TextWrapping.Wrap
                };
                Canvas.SetLeft(txtAd, x - aralik / 2);
                Canvas.SetTop(txtAd,  maxYukseklik + 28);
                cnvGrafik.Children.Add(txtAd);
            }
        }

        // ─── YARDIMCI ─────────────────────────────────────────────────
        private static void GosterMesaj(Border bd, TextBlock tb, string mesaj)
        {
            tb.Text = mesaj;
            bd.Visibility = Visibility.Visible;
        }

        private void btnKapat_Click(object sender, RoutedEventArgs e) => Close();
    }
}
