using System.Data;                             // DataTable, DataRow — классы для работы с табличными данными из БД
using LibraryApp.Database;                     // DatabaseHelper — наш класс для выполнения SQL-запросов к базе
using LibraryApp.Models;                       // User — модель данных пользователя (Id, Username,
                                               // ...)
using LibraryApp.Services;                     // UserService — сервис для операций: закладки, возврат, отзывы

namespace LibraryApp.Forms
{
    public partial class ProfileForm : Form     // partial — класс разделён на логику (.cs) и дизайн (.Designer.cs); Form — стандартное окно Windows
    {
        private User? _currentUser;              // ? — nullable (может быть null); текущий пользователь, чей профиль смотрим
        private readonly UserService _userService; // readonly — задаётся только в конструкторе; сервис для операций с БД от имени пользователя

        public ProfileForm()                     // Пустой конструктор — нужен для совместимости с дизайнером форм
        {
            InitializeComponent();               // Метод из Designer.cs — создаёт все кнопки, таблицы, надписи на форме
            _userService = new UserService();    // new — выделяет память под новый экземпляр сервиса пользователей
        }

        public ProfileForm(User user) : this()   // : this() — сначала вызывает пустой конструктор выше, потом этот
        {
            _currentUser = user;                 // Сохраняем переданного пользователя в поле класса
            SetupEvents();                       // Подписываемся на клики всех кнопок и выбор строк в таблицах
            LoadAllData();                       // Загружаем все данные: инфо, книги, закладки, историю
        }

        private void SetupEvents()               // Централизованная подписка на все события формы
        {
            btnReturnBook.Click += BtnReturnBook_Click;           // += добавляем обработчик клика на кнопку «Вернуть»
            btnCancelReservation.Click += BtnCancelReservation_Click; // Кнопка «Отменить бронь»
            btnRemoveBookmark.Click += BtnRemoveBookmark_Click;   // Кнопка «Убрать из закладок»
            btnClearHistory.Click += BtnClearHistory_Click;      // Кнопка «Очистить историю»
            dataGridViewLoanedBooks.SelectionChanged += DataGridViewLoanedBooks_SelectionChanged; // Событие: выбрали другую строку в таблице книг
        }

        private void LoadAllData()               // Загружает все четыре секции профиля
        {
            LoadUserInfo();                      // Верхняя часть: имя, email, дата регистрации
            LoadLoanedBooks();                   // Таблица «Мои книги»: взятые и забронированные
            LoadBookmarks();                     // Таблица «Закладки»
            LoadReadHistory();                   // Таблица «История»: прочитанные книги
        }

        private void LoadUserInfo()              // Заполняет верхнюю часть профиля данными пользователя
        {
            if (_currentUser == null) return;    // Защита: если пользователь не передан — нечего показывать
            lblUserName.Text = _currentUser.FullName; // Надпись с полным именем (например, «Иван Иванов»)
            lblEmail.Text = $"Email: {_currentUser.Email}"; // Надпись с почтой; $ — интерполяция строк (вставка переменной)
            lblRegistrationDate.Text = $"Дата регистрации: {_currentUser.RegistrationDate:dd.MM.yyyy}"; // :dd.MM.yyyy — формат даты «01.01.2024»
        }

        private void LoadLoanedBooks()           // Заполняет таблицу «Мои книги»
        {
            if (_currentUser == null) return;

            // SQL: получить все активные займы пользователя (выданные и забронированные книги)
            // INNER JOIN Books — подтягиваем название книги по BookId
            // INNER JOIN Authors — подтягиваем имя автора по AuthorId
            // WHERE Status IN ('Выдана', 'Забронирована') — только активные статусы
            // ORDER BY LoanDate DESC — сортировка: новые займы сверху
            string query = $@"SELECT l.LoanId, l.LoanDate, l.DueDate, b.Title, a.AuthorName, l.Status, 
                             l.IsOnline, l.ReservationCode
                             FROM Loans l 
                             INNER JOIN Books b ON l.BookId = b.BookId 
                             INNER JOIN Authors a ON b.AuthorId = a.AuthorId 
                             WHERE l.UserId = {_currentUser.UserId} 
                             AND l.Status IN (N'Выдана', N'Забронирована') 
                             ORDER BY l.LoanDate DESC";

            DataTable dt = DatabaseHelper.ExecuteQuery(query); // Отправляем SQL в БД, получаем таблицу с результатами
            dataGridViewLoanedBooks.Rows.Clear(); // Очищаем все старые строки перед загрузкой новых

            foreach (DataRow row in dt.Rows)     // DataRow — одна строка таблицы; перебираем все строки результата
            {
                dataGridViewLoanedBooks.Rows.Add( // Rows.Add — добавляем новую строку в таблицу на форме
                    Convert.ToDateTime(row["LoanDate"]).ToString("dd.MM.yyyy"),  // [0] Дата выдачи (object → DateTime → строка)
                    Convert.ToDateTime(row["DueDate"]).ToString("dd.MM.yyyy"),    // [1] Вернуть до
                    row["Title"]?.ToString() ?? "",                               // [2] Название книги; ?. — если null, пропустить; ?? — заменить на ""
                    row["AuthorName"]?.ToString() ?? "",                          // [3] Автор (из JOIN с Authors)
                    row["Status"]?.ToString() ?? "",                              // [4] Статус: «Выдана» или «Забронирована»
                    row["ReservationCode"]?.ToString() ?? ""                      // [5] Код бронирования (8 символов, только для печатных)
                );
            }

            UpdateButtons();                     // Обновляем кнопки «Вернуть» и «Отменить бронь» под таблицей
        }

        private void UpdateButtons()             // Управляет состоянием кнопок справа от таблицы
        {
            btnReturnBook.Enabled = false;       // По умолчанию кнопки неактивны (серые)
            btnReturnBook.Text = "Вернуть";
            btnReturnBook.BackColor = Color.Gray; // Серый цвет — неактивно
            btnCancelReservation.Enabled = false;
            btnCancelReservation.Visible = false;  // Кнопка отмены брони скрыта по умолчанию

            if (dataGridViewLoanedBooks.SelectedRows.Count == 0) return; // Ни одна строка не выбрана — выходим

            var row = dataGridViewLoanedBooks.SelectedRows[0]; // Первая выделенная строка таблицы
            string status = row.Cells[4].Value?.ToString() ?? ""; // Читаем значение из колонки [4] — Статус
            bool isOnline = false;               // По умолчанию считаем что книга печатная

            if (_currentUser != null)            // Если пользователь известен — проверяем тип книги
            {
                // SQL: получаем список всех IsOnline для активных займов пользователя
                string checkOnlineQuery = $@"SELECT l.IsOnline FROM Loans l 
                                            WHERE l.UserId = {_currentUser.UserId} 
                                            AND l.Status IN (N'Выдана', N'Забронирована')";
                var dtCheck = DatabaseHelper.ExecuteQuery(checkOnlineQuery);
                if (dtCheck.Rows.Count > dataGridViewLoanedBooks.SelectedRows[0].Index) // Проверяем что такая строка есть
                {
                    isOnline = Convert.ToBoolean(dtCheck.Rows[dataGridViewLoanedBooks.SelectedRows[0].Index]["IsOnline"]); // 0 → false, 1 → true
                }
            }

            if (isOnline && status == "Выдана")  // Онлайн-книга И статус «Выдана» — можно вернуть через приложение
            {
                btnReturnBook.Enabled = true;    // Делаем кнопку активной
                btnReturnBook.Text = "Вернуть";
                btnReturnBook.BackColor = Color.FromArgb(231, 76, 60); // Красный цвет — действие «вернуть»
                btnCancelReservation.Visible = false; // Кнопку отмены брони скрываем (уже выдана)
            }
            else if (status == "Забронирована")  // Книга только забронирована, ещё не выдана
            {
                btnReturnBook.Enabled = false;   // Вернуть нельзя — ещё не выдана
                btnReturnBook.Text = "Ждет выдачи";
                btnReturnBook.BackColor = Color.Gray;
                btnCancelReservation.Visible = true; // Показываем кнопку отмены брони
                btnCancelReservation.Enabled = true;
                btnCancelReservation.Text = "Отменить бронь";
                btnCancelReservation.BackColor = Color.FromArgb(230, 126, 34); // Оранжевый — предупреждающий цвет
            }
            else if (!isOnline && status == "Выдана") // Печатная книга на руках — вернуть может только библиотекарь
            {
                btnReturnBook.Enabled = false;
                btnReturnBook.Text = "У библиотекаря"; // Подсказка пользователю
                btnReturnBook.BackColor = Color.Gray;
                btnCancelReservation.Visible = false;
            }
        }

        private void DataGridViewLoanedBooks_SelectionChanged(object? sender, EventArgs e) // Пользователь кликнул на другую строку
        {
            UpdateButtons();                     // Пересчитываем доступность кнопок
        }

        private void BtnReturnBook_Click(object? sender, EventArgs e) // Нажата кнопка «Вернуть» — возврат онлайн-книги
        {
            if (_currentUser == null) return;
            if (dataGridViewLoanedBooks.SelectedRows.Count == 0) // Ничего не выбрано
            {
                MessageBox.Show("Выберите книгу для возврата.", "Предупреждение",
                MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
            }

            var row = dataGridViewLoanedBooks.SelectedRows[0];
            string title = row.Cells[2].Value?.ToString() ?? ""; // Читаем название книги из колонки [2]

            // SQL: ищем loanId, bookId и IsOnline по названию книги и пользователю
            // INNER JOIN Books — нужен для поиска по названию (Title)
            string findQuery = $@"SELECT l.LoanId, l.BookId, l.IsOnline FROM Loans l 
                                 INNER JOIN Books b ON l.BookId = b.BookId 
                                 WHERE l.UserId = {_currentUser.UserId} 
                                 AND b.Title = N'{title.Replace("'", "''")}'  -- N — Unicode; Replace — экранируем кавычки
                                 AND l.Status = N'Выдана'"; // Только активные выдачи
            var dt = DatabaseHelper.ExecuteQuery(findQuery);

            if (dt.Rows.Count == 0)              // Книга не найдена (возможно, уже возвращена)
            { MessageBox.Show("Книга не найдена.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            bool isOnline = Convert.ToBoolean(dt.Rows[0]["IsOnline"]); // Читаем признак онлайн-книги
            int loanId = Convert.ToInt32(dt.Rows[0]["LoanId"]);       // ID займа для обновления
            int bookId = Convert.ToInt32(dt.Rows[0]["BookId"]);       // ID книги для истории

            if (!isOnline)                       // Печатную книгу нельзя вернуть через приложение
            {
                MessageBox.Show("Печатные книги возвращайте библиотекарю.", "Информация",
                MessageBoxButtons.OK, MessageBoxIcon.Information); return;
            }

            if (MessageBox.Show($"Вернуть книгу «{title}»?", "Подтверждение", // Диалог подтверждения
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            // SQL: меняем статус займа на «Возвращена» и записываем текущую дату возврата
            string returnQuery = $@"UPDATE Loans SET Status = N'Возвращена', ReturnDate = GETDATE() 
                                   WHERE LoanId = {loanId} AND Status = N'Выдана'"; // GETDATE() — текущая дата/время на сервере
            DatabaseHelper.ExecuteNonQuery(returnQuery); // Выполняем UPDATE (не возвращает строки)

            // SQL: добавляем книгу в историю прочитанных, если её там ещё нет
            string historyQuery = $@"IF NOT EXISTS (SELECT 1 FROM ReadHistory WHERE UserId = {_currentUser.UserId} AND BookId = {bookId})
                                     INSERT INTO ReadHistory (UserId, BookId) VALUES ({_currentUser.UserId}, {bookId})";
            // IF NOT EXISTS — проверка: если записи ещё нет, тогда INSERT
            DatabaseHelper.ExecuteNonQuery(historyQuery);

            MessageBox.Show("Книга возвращена!", "Успешно", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadAllData();                       // Перезагружаем все данные профиля
        }

        private void BtnCancelReservation_Click(object? sender, EventArgs e) // Отмена бронирования пользователем
        {
            if (_currentUser == null) return;
            if (dataGridViewLoanedBooks.SelectedRows.Count == 0)
            { MessageBox.Show("Выберите книгу.", "Предупреждение", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            var row = dataGridViewLoanedBooks.SelectedRows[0];
            string status = row.Cells[4].Value?.ToString() ?? ""; // [4] Статус
            string title = row.Cells[2].Value?.ToString() ?? "";  // [2] Название

            if (status != "Забронирована")       // Защита: отменить можно только статус «Забронирована»
            {
                MessageBox.Show("Можно отменить только бронь.", "Информация",
                MessageBoxButtons.OK, MessageBoxIcon.Information); return;
            }

            if (MessageBox.Show($"Отменить бронирование книги «{title}»?", "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            // Находим bookId по названию книги
            string findBookQuery = $"SELECT BookId FROM Books WHERE Title = N'{title.Replace("'", "''")}'";
            var bookResult = DatabaseHelper.ExecuteScalar(findBookQuery); // ExecuteScalar — возвращает одно значение (первую ячейку)
            if (bookResult == null || bookResult == DBNull.Value) return; // DBNull.Value — NULL в терминах базы данных
            int bookId = Convert.ToInt32(bookResult); // object → int

            // SQL: меняем статус займа на «Отменена»
            string cancelQuery = $@"UPDATE Loans SET Status = N'Отменена', ReturnDate = GETDATE() 
                                   WHERE UserId = {_currentUser.UserId} 
                                   AND BookId = {bookId} 
                                   AND Status = N'Забронирована'";
            DatabaseHelper.ExecuteNonQuery(cancelQuery);

            // SQL: удаляем запись из BookPickups (таблица выдачи библиотекарем)
            string deletePickupQuery = $@"DELETE FROM BookPickups 
                                         WHERE BookId = {bookId} AND UserId = {_currentUser.UserId} 
                                         AND Status = N'Ожидает'";
            DatabaseHelper.ExecuteNonQuery(deletePickupQuery);

            // SQL: возвращаем копию книги в доступные и делаем книгу доступной для бронирования
            string updateBookQuery = $@"UPDATE Books SET AvailableCopies = AvailableCopies + 1, IsAvailable = 1 
                                       WHERE BookId = {bookId}";
            // AvailableCopies + 1 — увеличиваем счётчик доступных копий на 1
            // IsAvailable = 1 — книга снова доступна для бронирования
            DatabaseHelper.ExecuteNonQuery(updateBookQuery);

            MessageBox.Show("Бронирование отменено!", "Успешно", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadAllData();                       // Обновляем все секции профиля
        }

        private void LoadBookmarks()             // Загружает таблицу «Закладки»
        {
            if (_currentUser == null) return;

            // SQL: получить все закладки пользователя с информацией о книге
            // INNER JOIN Books — название книги и рейтинг
            // INNER JOIN Authors — имя автора
            // INNER JOIN Genres — название жанра
            // ORDER BY DateAdded DESC — новые закладки сверху
            string query = $@"SELECT b.Title, a.AuthorName, g.GenreName, b.Rating, bm.DateAdded
                             FROM Bookmarks bm 
                             INNER JOIN Books b ON bm.BookId = b.BookId 
                             INNER JOIN Authors a ON b.AuthorId = a.AuthorId 
                             INNER JOIN Genres g ON b.GenreId = g.GenreId 
                             WHERE bm.UserId = {_currentUser.UserId} 
                             ORDER BY bm.DateAdded DESC";

            DataTable dt = DatabaseHelper.ExecuteQuery(query);
            dataGridViewBookmarks.Rows.Clear();  // Очищаем старые строки

            foreach (DataRow row in dt.Rows)
            {
                dataGridViewBookmarks.Rows.Add(
                    row["Title"]?.ToString() ?? "",                               // [0] Название книги
                    row["AuthorName"]?.ToString() ?? "",                          // [1] Автор
                    row["GenreName"]?.ToString() ?? "",                           // [2] Жанр
                    $"{Convert.ToDecimal(row["Rating"]):F1}",                     // [3] Рейтинг с одной цифрой после запятой
                    Convert.ToDateTime(row["DateAdded"]).ToString("dd.MM.yyyy")   // [4] Дата добавления в закладки
                );
            }
        }

        private void BtnRemoveBookmark_Click(object? sender, EventArgs e) // Удаление книги из закладок
        {
            if (_currentUser == null) return;
            if (dataGridViewBookmarks.SelectedRows.Count == 0)
            {
                MessageBox.Show("Выберите книгу из закладок.", "Предупреждение",
                MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
            }

            string title = dataGridViewBookmarks.SelectedRows[0].Cells[0].Value?.ToString() ?? ""; // [0] Название выбранной книги

            if (MessageBox.Show($"Удалить «{title}» из закладок?", "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            // SQL: удаляем закладку по UserId и BookId (BookId находим по названию через подзапрос)
            string query = $@"DELETE FROM Bookmarks 
                             WHERE UserId = {_currentUser.UserId} 
                             AND BookId = (SELECT BookId FROM Books WHERE Title = N'{title.Replace("'", "''")}')";
            // Подзапрос в скобках выполняется первым и возвращает BookId для найденной книги
            DatabaseHelper.ExecuteNonQuery(query);

            MessageBox.Show("Удалено из закладок.", "Успешно", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadBookmarks();                     // Перезагружаем таблицу закладок
        }

        private void LoadReadHistory()           // Загружает таблицу «История прочитанных книг»
        {
            if (_currentUser == null) return;

            // SQL: получить историю чтения пользователя
            // INNER JOIN Books — название книги и рейтинг
            // INNER JOIN Authors — имя автора
            // ORDER BY ReadDate DESC — недавно прочитанные сверху
            string query = $@"SELECT rh.ReadDate, b.Title, a.AuthorName, b.Rating 
                             FROM ReadHistory rh 
                             INNER JOIN Books b ON rh.BookId = b.BookId 
                             INNER JOIN Authors a ON b.AuthorId = a.AuthorId 
                             WHERE rh.UserId = {_currentUser.UserId} 
                             ORDER BY rh.ReadDate DESC";

            DataTable dt = DatabaseHelper.ExecuteQuery(query);
            dataGridViewReadHistory.Rows.Clear();

            foreach (DataRow row in dt.Rows)
            {
                dataGridViewReadHistory.Rows.Add(
                    Convert.ToDateTime(row["ReadDate"]).ToString("dd.MM.yyyy"),   // [0] Дата прочтения
                    row["Title"]?.ToString() ?? "",                               // [1] Название книги
                    row["AuthorName"]?.ToString() ?? "",                          // [2] Автор
                    $"{Convert.ToDecimal(row["Rating"]):F1}"                      // [3] Рейтинг на момент прочтения
                );
            }
        }

        private void BtnClearHistory_Click(object? sender, EventArgs e) // Полная очистка истории чтения
        {
            if (_currentUser == null) return;

            if (dataGridViewReadHistory.Rows.Count == 0) // Нечего очищать
            {
                MessageBox.Show("История пуста.", "Информация",
                MessageBoxButtons.OK, MessageBoxIcon.Information); return;
            }

            if (MessageBox.Show("Очистить всю историю прочитанных книг?", "Подтверждение", // Диалог с предупреждением
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            string query = $"DELETE FROM ReadHistory WHERE UserId = {_currentUser.UserId}"; // Удаляем ВСЕ записи истории пользователя
            DatabaseHelper.ExecuteNonQuery(query);

            MessageBox.Show("История очищена!", "Успешно", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadReadHistory();                   // Перезагружаем таблицу (теперь она будет пустой)
        }
    }
}