using LibraryApp.Database;                     // DatabaseHelper — выполнение SQL-запросов
using LibraryApp.Models;                       // Book, User — модели данных
using LibraryApp.Services;                     // BookService, RecommendationService, AdminService

namespace LibraryApp.Forms
{
    public partial class MainForm : Form       // partial — класс разделён на логику и дизайн; Form — окно Windows
    {
        private readonly BookService _bookService;                // Сервис для получения популярных и новых книг
        private readonly RecommendationService _recommendationService; // Сервис для рекомендаций (последние добавленные)
        private readonly AdminService _adminService;              // Сервис для загрузки обложек из БД
        private readonly User _currentUser;                       // Текущий вошедший пользователь
        private bool _isLoggingOut = false;                       // Флаг: true — выход в LoginForm, false — закрытие приложения

        public MainForm(User user)                               // Конструктор: user — пользователь после входа
        {
            InitializeComponent();                                // Создаёт все элементы формы (метод из Designer.cs)
            _currentUser = user;                                 // Сохраняем пользователя для персонализации
            _bookService = new BookService();                    // Сервис книг (популярные, новинки)
            _recommendationService = new RecommendationService(); // Сервис рекомендаций
            _adminService = new AdminService();                  // Сервис для обложек

            LoadUserInfo();                                      // Показываем приветствие и роль
            CheckAdminAccess();                                  // Показываем кнопку «Админ» для сотрудников
            SetupEvents();                                       // Подписываемся на все клики и события
            LoadData();                                          // Загружаем книги, рекомендации
        }

        private void LoadUserInfo()                              // Отображение приветствия и роли
        {
            lblWelcome.Text = $"Добро пожаловать, {_currentUser.FullName}"; // «Добро пожаловать, Иван Иванов»
            string roleEmoji = _currentUser.RoleId switch        // switch — выбор эмодзи по ID роли
            {
                1 => "👑",                                       // Admin
                2 => "📋",                                       // Librarian
                _ => "👤"                                        // _ — все остальные (User)
            };
            lblRole.Text = $"{roleEmoji} {_currentUser.RoleName}"; // «👑 Администратор»
        }

        private void CheckAdminAccess() => btnAdminPanel.Visible = _currentUser.CanManageLibrary; // CanManageLibrary — true для Admin и Librarian

        private void SetupEvents()                               // Подписка на все события формы
        {
            btnCatalog.Click += (s, e) => OpenCatalog();          // Кнопка «Каталог»
            btnProfile.Click += (s, e) => OpenProfile();          // Кнопка «Профиль»
            btnAdminPanel.Click += (s, e) => OpenAdmin();         // Кнопка «Админ» (только для сотрудников)
            btnLogout.Click += (s, e) => Logout();                // Кнопка «Выход»
            btnQuickSearch.Click += (s, e) => QuickSearch();      // Кнопка «Найти» (быстрый поиск)
            btnQuickSearchAuthor.Click += (s, e) => SearchBy("author"); // Поиск по автору
            btnQuickSearchGenre.Click += (s, e) => SearchBy("genre");   // Поиск по жанру
            btnQuickSearchTitle.Click += (s, e) => SearchBy("title");   // Поиск по названию
            textBoxQuickSearch.KeyPress += (s, e) =>              // Нажатие клавиши в поле быстрого поиска
            {
                if (e.KeyChar == (char)Keys.Enter)                // Нажат Enter
                { QuickSearch(); e.Handled = true; }              // Выполняем поиск, e.Handled — не передавать дальше
            };
            FormClosing += (s, e) =>                               // Перед закрытием формы
            {
                if (_isLoggingOut) return;                        // Выход в LoginForm — не спрашиваем
                if (e.CloseReason == CloseReason.UserClosing)      // Закрытие пользователем (крестик)
                {
                    if (MessageBox.Show("Выйти из программы?", "",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                        e.Cancel = true;                          // Нет — отменяем закрытие
                    else Application.Exit();                      // Да — полный выход
                }
            };
            FormClosed += (s, e) =>                                // После закрытия формы
            {
                if (_isLoggingOut)                                 // Выход в LoginForm
                {
                    var loginForm = Application.OpenForms.OfType<LoginForm>().FirstOrDefault(); // Ищем существующую форму входа
                    if (loginForm != null) { loginForm.Show(); loginForm.Focus(); } // Показываем найденную
                    else new LoginForm().Show();                   // Или создаём новую
                }
                else Application.Exit();                           // Полный выход
            };
        }

        private void LoadData()                                    // Загрузка всех данных
        {
            LoadPopularBooks();                                    // Хиты чтения (вкладка 1)
            LoadNewBooks();                                        // Новинки (вкладка 2)
            LoadRecommendations();                                 // Рекомендуемые (вкладка 3)
            LoadTrendingBooks();                                   // Последние добавленные в списке
        }

        private void LoadPopularBooks()                            // Загрузка популярных книг
        {
            flowLayoutPopular.Controls.Clear();                    // Очищаем старые карточки
            foreach (var book in _bookService.GetPopularBooks(10)) // 10 хитов
                flowLayoutPopular.Controls.Add(CreateBookCardFromTemplate(book)); // Клонируем шаблон
        }

        private void LoadNewBooks()                                // Загрузка новинок
        {
            flowLayoutNew.Controls.Clear();
            foreach (var book in _bookService.GetNewBooks(10))
                flowLayoutNew.Controls.Add(CreateBookCardFromTemplate(book));
        }

        private void LoadRecommendations()                         // Загрузка последних добавленных
        {
            flowLayoutRecommended.Controls.Clear();
            var recommendations = _recommendationService.GetPersonalRecommendations(_currentUser.UserId); // Последние добавленные
            lblRecommendationInfo.Text = "📚 Последние добавленные книги"; // Заголовок секции
            foreach (var book in recommendations)
                flowLayoutRecommended.Controls.Add(CreateBookCardFromTemplate(book));
        }

        private void LoadTrendingBooks()                           // Загрузка списка последних книг
        {
            listBoxRecommendations.Items.Clear();
            var trendingBooks = _recommendationService.GetTrendingBooks(); // Последние добавленные
            foreach (var book in trendingBooks)
                listBoxRecommendations.Items.Add($"🆕 {book.Title} - {book.AuthorName} (Добавлено: {book.DateAdded:dd.MM.yyyy})"); // Формат: дата
        }

        private Panel CreateBookCardFromTemplate(Book book)        // Создание карточки из шаблона
        {
            Panel card = ClonePanel(cardTemplate);                 // Клонируем шаблон

            Panel cover = (Panel)card.Controls["coverTemplate"];   // Панель обложки (по имени)
            Label coverIcon = (Label)cover.Controls["coverIconTemplate"]; // Иконка-заглушка 📚
            Label title = (Label)card.Controls["titleTemplate"];   // Название
            Label author = (Label)card.Controls["authorTemplate"]; // Автор
            Label rating = (Label)card.Controls["ratingTemplate"]; // Рейтинг
            Label status = (Label)card.Controls["statusTemplate"]; // Статус
            Button detail = (Button)card.Controls["detailTemplate"]; // Кнопка «Подробнее»

            var coverData = _adminService.GetBookCover(book.BookId); // Загружаем обложку из БД
            if (coverData != null && coverData.Length > 0)         // Обложка существует
            {
                try
                {
                    using var ms = new MemoryStream(coverData);    // Поток в памяти
                    var pb = new PictureBox
                    {
                        Image = Image.FromStream(ms),              // Картинка из потока
                        SizeMode = PictureBoxSizeMode.Zoom,        // Масштабирование
                        Dock = DockStyle.Fill                      // На весь coverTemplate
                    };
                    cover.Controls.Clear();                        // Удаляем иконку-заглушку
                    cover.BackColor = Color.Transparent;           // Убираем синий фон
                    cover.Controls.Add(pb);                        // Добавляем картинку
                }
                catch { coverIcon.Text = "📚"; }                   // Ошибка — оставляем заглушку
            }

            title.Text = book.Title.Length > 25 ? book.Title[..22] + "..." : book.Title; // Обрезаем длинные названия
            author.Text = book.AuthorName;                         // Имя автора
            rating.Text = $"⭐ {book.Rating:F1}";                  // Рейтинг со звездой
            status.Text = book.IsAvailable || book.BookType == "Онлайн" ? "✅ Доступна" : "❌ На руках"; // Статус
            status.ForeColor = book.IsAvailable || book.BookType == "Онлайн" ? Color.Green : Color.Red; // Цвет статуса

            detail.Tag = book.BookId;                              // Сохраняем ID книги в кнопке
            detail.Click += (s, e) =>                               // Клик по «Подробнее»
            {
                if (s is Button btn && btn.Tag is int bId)         // Извлекаем ID из Tag
                {
                    using var form = new BookDetailForm(bId, _currentUser.UserId); // Открываем карточку
                    form.ShowDialog();
                    LoadData();                                    // Обновляем данные после возврата
                }
            };

            return card;
        }

        private Panel ClonePanel(Panel source)                     // Шаблон карточки миниатюры книги
        {
            Panel clone = new Panel
            {
                Width = source.Width,
                Height = source.Height,      // Размеры
                BorderStyle = source.BorderStyle,                  // Стиль рамки
                BackColor = source.BackColor,                      // Цвет фона
                Margin = source.Margin,
                Padding = source.Padding   // Отступы
            };
            foreach (Control control in source.Controls)           // Копируем все дочерние элементы
                clone.Controls.Add(CloneControl(control));         // Рекурсивное клонирование
            return clone;
        }

        private Control CloneControl(Control source)               // Клонирование любого контрола по типу
        {
            Control clone;

            if (source is Panel srcPanel)                          // Если Panel
            {
                clone = new Panel
                {
                    Width = srcPanel.Width,
                    Height = srcPanel.Height,
                    Location = srcPanel.Location,
                    BackColor = srcPanel.BackColor,
                    BorderStyle = srcPanel.BorderStyle,
                    Name = srcPanel.Name,
                    Dock = srcPanel.Dock
                };
                foreach (Control child in srcPanel.Controls)      
                    clone.Controls.Add(CloneControl(child));
            }
            else if (source is Label srcLabel)                     // Если Label
            {
                clone = new Label
                {
                    Text = srcLabel.Text,
                    Location = srcLabel.Location,
                    AutoSize = srcLabel.AutoSize,
                    Font = srcLabel.Font,
                    ForeColor = srcLabel.ForeColor,
                    BackColor = srcLabel.BackColor,
                    TextAlign = srcLabel.TextAlign,
                    Dock = srcLabel.Dock,
                    Name = srcLabel.Name
                };
                if (!srcLabel.AutoSize) clone.Size = srcLabel.Size; // Фиксированный размер
            }
            else if (source is Button srcButton)                   // Если Button
            {
                clone = new Button
                {
                    Text = srcButton.Text,
                    Location = srcButton.Location,
                    Size = srcButton.Size,
                    Font = srcButton.Font,
                    BackColor = srcButton.BackColor,
                    ForeColor = srcButton.ForeColor,
                    FlatStyle = srcButton.FlatStyle,
                    Name = srcButton.Name
                };
            }
            else                                                   // Все остальные типы
            {
                clone = new Control { Location = source.Location, Size = source.Size, Name = source.Name };
            }

            return clone;
        }

        private void OpenCatalog() { using var f = new CatalogForm(_currentUser.UserId); f.ShowDialog(); LoadData(); } // Открыть каталог
        private void OpenProfile() { using var f = new ProfileForm(_currentUser); f.ShowDialog(); LoadData(); }       // Открыть профиль
        private void OpenAdmin() { using var f = new AdminForm(_currentUser); f.ShowDialog(); LoadData(); }           // Открыть админ-панель

        private void Logout()                                     // Выход из системы
        {
            if (MessageBox.Show("Выйти из системы?", "",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _isLoggingOut = true;                                  // Флаг: выход в LoginForm
            Close();                                               // Закрываем форму
        }

        private void QuickSearch() { if (!string.IsNullOrWhiteSpace(textBoxQuickSearch.Text)) { using var f = new SearchForm("quick", _currentUser.UserId); f.ShowDialog(); } } // Быстрый поиск
        private void SearchBy(string type) { using var f = new SearchForm(type, _currentUser.UserId); f.ShowDialog(); } // Поиск по типу

        private void btnAdminPanel_Click(object sender, EventArgs e)
        {
            // Пустой обработчик, созданный дизайнером (не используется)
        }

        private void groupRecommendations_Enter(object sender, EventArgs e)
        {

        }
    }
}