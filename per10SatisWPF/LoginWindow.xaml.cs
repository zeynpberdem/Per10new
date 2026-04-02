using System.Windows;
using System.Windows.Input;

namespace per10SatisWPF
{
    public partial class LoginWindow : Window
    {
        private readonly string _kullanici = "peronyıkama";
        private readonly string _sifre = "per1023";

        public LoginWindow()
        {
            InitializeComponent();
            txtKullanici.Focus();
        }

        private void btnGiris_Click(object sender, RoutedEventArgs e) => TryLogin();

        private void txtSifre_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TryLogin();
        }

        private void TryLogin()
        {
            if (txtKullanici.Text == _kullanici && txtSifre.Password == _sifre)
            {
                Hide();
                var main = new MainWindow();
                main.Show();
                Close();
            }
            else
            {
                lblHata.Text = "Hatalı kullanıcı adı veya şifre!";
                lblHata.Visibility = Visibility.Visible;
                txtSifre.Clear();
                txtSifre.Focus();
            }
        }
    }
}
