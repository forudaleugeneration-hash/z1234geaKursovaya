using System.Data;                             // DataTable, DataRow — работа с результатами SQL-запросов
using LibraryApp.Database;                     // DatabaseHelper — выполнение запросов к базе данных
using LibraryApp.Models;                       // User, Loan, Book — модели данных

namespace LibraryApp.Services
{
    public class UserService                   // Сервис для операций пользователя: вход, регистрация, займы, закладки, отзывы
    {
        public User? Login(string username, string password) // Проверка логина и пароля; ? — может вернуть null если не найден
        {
            // SELECT * FROM Users — все колонки пользователя
            // WHERE Username = ... AND Password = ... AND IsActive = 1 — проверяем логин, пароль и что не заблокирован
            // N'...' — Unicode (NVARCHAR); Replace("'","''") — экранирование кавычек
            string query = $@"SELECT * FROM Users 
                             WHERE Username = N'{username.Replace("'", "''")}' 
                             AND Password = N'{password.Replace("'", "''")}' AND IsActive = 1";
            var dt = DatabaseHelper.ExecuteQuery(query); // Выполняем запрос
            if (dt.Rows.Count > 0)                       // Если нашли пользователя
            {
                var row = dt.Rows[0];                    // Берём первую (и единственную) строку
                return new User                          // Создаём объект User из строки
                {
                    UserId = Convert.ToInt32(row["UserId"]),              // object → int (ID)
                    Username = row["Username"]?.ToString() ?? string.Empty, // object → string (логин)
                    Password = "",                                       // Пароль НЕ сохраняем в объекте (безопасность)
                    Email = row["Email"]?.ToString() ?? string.Empty,    // object → string (почта)
                    FullName = row["FullName"]?.ToString() ?? string.Empty, // object → string (полное имя)
                    RegistrationDate = Convert.ToDateTime(row["RegistrationDate"]), // object → DateTime
                    IsActive = true,                                     // Пользователь активен (прошёл проверку)
                    RoleId = row["RoleId"] != DBNull.Value               // Если RoleId не NULL в БД
                        ? Convert.ToInt32(row["RoleId"])                 // Берём значение
                        : 3                                              // Иначе — роль "User" по умолчанию
                };
            }
            return null;                             // Пользователь не найден — возвращаем null
        }

        public bool Register(string username, string password, string email = "", string fullName = "") // Регистрация; = "" — параметры по умолчанию
        {
            // SELECT COUNT(*) FROM Users WHERE Username = ... — проверяем, нет ли уже такого логина
            var result = DatabaseHelper.ExecuteScalar($"SELECT COUNT(*) FROM Users WHERE Username = N'{username.Replace("'", "''")}'");
            if (result != null && Convert.ToInt32(result) > 0) // Если COUNT > 0 — логин занят
                return false;                                  // Возвращаем false — регистрация не удалась

            // INSERT INTO Users (...) VALUES (...) — добавляем нового пользователя
            // RoleId = 3 — по умолчанию роль "User"
            string query = $@"INSERT INTO Users (Username, Password, Email, FullName, RoleId) 
                             VALUES (N'{username.Replace("'", "''")}', N'{password.Replace("'", "''")}', 
                                     N'{email.Replace("'", "''")}', N'{fullName.Replace("'", "''")}', 3)";
            return DatabaseHelper.ExecuteNonQuery(query) > 0; // > 0 — если добавили хотя бы одну строку → true
        }

        public List<Loan> GetActiveLoans(int userId) // Получить активные займы пользователя
        {
            var loans = new List<Loan>();
            // l.* — все колонки из Loans
            // b.Title — название книги из Books (через JOIN)
            // a.AuthorName — имя автора из Authors (через JOIN)
            // b.BookType — тип книги (Печатная/Онлайн)
            // CASE WHEN b.PdfContent IS NOT NULL THEN 1 ELSE 0 END as HasPdf — вычисляемое поле: есть ли PDF (1/0)
            // INNER JOIN Books b ON l.BookId = b.BookId — соединяем Loans с Books
            // INNER JOIN Authors a ON b.AuthorId = a.AuthorId — соединяем Books с Authors
            // WHERE l.UserId = ... AND Status IN ('Выдана', 'Забронирована') — только активные займы
            // ORDER BY l.LoanDate DESC — сортировка: новые сверху
            string query = $@"SELECT l.*, b.Title, a.AuthorName, b.BookType, 
                             CASE WHEN b.PdfContent IS NOT NULL THEN 1 ELSE 0 END as HasPdf
                             FROM Loans l 
                             INNER JOIN Books b ON l.BookId = b.BookId 
                             INNER JOIN Authors a ON b.AuthorId = a.AuthorId 
                             WHERE l.UserId = {userId} AND l.Status IN (N'Выдана', N'Забронирована') 
                             ORDER BY l.LoanDate DESC";
            var dt = DatabaseHelper.ExecuteQuery(query);
            foreach (DataRow row in dt.Rows)             // Перебираем строки результата
            {
                loans.Add(new Loan                       // Создаём объект Loan из строки
                {
                    LoanId = Convert.ToInt32(row["LoanId"]),              // ID займа
                    BookId = Convert.ToInt32(row["BookId"]),              // ID книги
                    UserId = userId,                                     // ID пользователя (из параметра)
                    BookTitle = row["Title"]?.ToString() ?? string.Empty, // Название книги (из JOIN)
                    AuthorName = row["AuthorName"]?.ToString() ?? string.Empty, // Имя автора (из JOIN)
                    LoanDate = Convert.ToDateTime(row["LoanDate"]),      // Дата займа
                    DueDate = Convert.ToDateTime(row["DueDate"]),        // Дата возврата
                    Status = row["Status"]?.ToString() ?? "Выдана",      // Статус: Выдана/Забронирована
                    IsOnline = row["IsOnline"] != DBNull.Value && Convert.ToBoolean(row["IsOnline"]), // Онлайн-книга?
                    BookType = row.Table.Columns.Contains("BookType")    // Тип книги (из JOIN)
                        ? row["BookType"]?.ToString() ?? "Печатная" : "Печатная",
                    HasPdf = row.Table.Columns.Contains("HasPdf") && Convert.ToInt32(row["HasPdf"]) == 1, // Есть PDF? (1 → true)
                    ReservationCode = row.Table.Columns.Contains("ReservationCode") // Код бронирования (может быть null)
                        ? row["ReservationCode"]?.ToString() : null
                });
            }
            return loans;
        }

        public List<Book> GetReadHistory(int userId) // Получить историю прочитанных книг
        {
            var books = new List<Book>();
            // b.* — все колонки Books; a.AuthorName — автор; g.GenreName — жанр; rh.ReadDate — дата прочтения
            // INNER JOIN Books b ON rh.BookId = b.BookId — соединяем ReadHistory с Books
            // INNER JOIN Authors a ON b.AuthorId = a.AuthorId — соединяем Books с Authors
            // INNER JOIN Genres g ON b.GenreId = g.GenreId — соединяем Books с Genres
            // ORDER BY rh.ReadDate DESC — недавно прочитанные сверху
            string query = $@"SELECT b.*, a.AuthorName, g.GenreName, rh.ReadDate 
                             FROM ReadHistory rh 
                             INNER JOIN Books b ON rh.BookId = b.BookId 
                             INNER JOIN Authors a ON b.AuthorId = a.AuthorId 
                             INNER JOIN Genres g ON b.GenreId = g.GenreId 
                             WHERE rh.UserId = {userId} ORDER BY rh.ReadDate DESC";
            var dt = DatabaseHelper.ExecuteQuery(query);
            var bookService = new BookService();         // Сервис для MapBook()
            foreach (DataRow row in dt.Rows)             // Перебираем строки
                books.Add(bookService.MapBook(row));     // DataRow → Book через MapBook
            return books;
        }

        public List<Book> GetBookmarks(int userId)   // Получить список закладок пользователя
        {
            var books = new List<Book>();
            // b.* — все колонки Books; a.AuthorName — автор; g.GenreName — жанр
            // FROM Bookmarks bm — основная таблица
            // INNER JOIN Books b ON bm.BookId = b.BookId — соединяем закладки с книгами
            // INNER JOIN Authors a ON b.AuthorId = a.AuthorId — соединяем книги с авторами
            // INNER JOIN Genres g ON b.GenreId = g.GenreId — соединяем книги с жанрами
            // ORDER BY bm.DateAdded DESC — недавние закладки сверху
            string query = $@"SELECT b.*, a.AuthorName, g.GenreName 
                             FROM Bookmarks bm 
                             INNER JOIN Books b ON bm.BookId = b.BookId 
                             INNER JOIN Authors a ON b.AuthorId = a.AuthorId 
                             INNER JOIN Genres g ON b.GenreId = g.GenreId 
                             WHERE bm.UserId = {userId} ORDER BY bm.DateAdded DESC";
            var dt = DatabaseHelper.ExecuteQuery(query);
            var bookService = new BookService();
            foreach (DataRow row in dt.Rows) books.Add(bookService.MapBook(row)); // DataRow → Book
            return books;
        }

        public string BorrowBook(int userId, int bookId) // Взять книгу (онлайн — сразу, печатная — бронь)
        {
            var bookService = new BookService();
            var book = bookService.GetBookById(bookId);  // Проверяем существование книги
            if (book == null) return string.Empty;        // Книга не найдена

            if (book.BookType == "Онлайн")               // Онлайн-книга — выдаём сразу
            {
                var dueDate = DateTime.Now.AddDays(30);   // На 30 дней
                // INSERT INTO Loans — создаём займ со статусом «Выдана», IsOnline = 1
                string query = $@"INSERT INTO Loans (UserId, BookId, DueDate, Status, IsOnline) 
                                 VALUES ({userId}, {bookId}, '{dueDate:yyyy-MM-dd}', N'Выдана', 1)";
                DatabaseHelper.ExecuteNonQuery(query);
                // UPDATE Books SET Popularity = Popularity + 1 — увеличиваем счётчик популярности
                DatabaseHelper.ExecuteNonQuery($"UPDATE Books SET Popularity = Popularity + 1 WHERE BookId = {bookId}");
                return "ONLINE";                          // Специальный ответ: онлайн-книга взята
            }
            else                                         // Печатная книга — создаём бронь
            {
                if (book.AvailableCopies <= 0) return "NO_COPIES"; // Нет доступных копий
                var adminService = new AdminService();
                string code = adminService.CreateReservation(userId, bookId); // Создаём бронь с кодом
                // UPDATE Books SET Popularity = Popularity + 1 — увеличиваем счётчик популярности
                DatabaseHelper.ExecuteNonQuery($"UPDATE Books SET Popularity = Popularity + 1 WHERE BookId = {bookId}");
                return code;                              // Возвращаем 8-значный код бронирования
            }
        }

        public bool ReturnBook(int userId, int bookId)   // Вернуть книгу (только онлайн)
        {
            // SELECT l.LoanId, l.IsOnline FROM Loans — ищем активный займ
            // WHERE UserId = ... AND BookId = ... AND Status = 'Выдана' — только выданные
            string checkQuery = $@"SELECT l.LoanId, l.IsOnline FROM Loans l 
                                  WHERE l.UserId = {userId} AND l.BookId = {bookId} AND l.Status = N'Выдана'";
            var dt = DatabaseHelper.ExecuteQuery(checkQuery);
            if (dt.Rows.Count == 0) return false;         // Активный займ не найден

            bool isOnline = Convert.ToBoolean(dt.Rows[0]["IsOnline"]); // Проверяем тип книги
            if (!isOnline) return false;                  // Печатную нельзя вернуть через приложение

            int loanId = Convert.ToInt32(dt.Rows[0]["LoanId"]); // ID займа для обновления
            // UPDATE Loans SET Status = 'Возвращена', ReturnDate = GETDATE() — обновляем статус и дату возврата
            DatabaseHelper.ExecuteNonQuery($"UPDATE Loans SET Status = N'Возвращена', ReturnDate = GETDATE() WHERE LoanId = {loanId}");
            // IF NOT EXISTS (...) INSERT INTO ReadHistory — добавляем в историю если ещё нет
            DatabaseHelper.ExecuteNonQuery($@"IF NOT EXISTS (SELECT 1 FROM ReadHistory WHERE UserId = {userId} AND BookId = {bookId})
                                             INSERT INTO ReadHistory (UserId, BookId) VALUES ({userId}, {bookId})");
            return true;                                 // Успешно возвращена
        }

        public void AddBookmark(int userId, int bookId) // Добавить книгу в закладки
        {
            // SELECT COUNT(*) FROM Bookmarks — проверяем, нет ли уже такой закладки
            var result = DatabaseHelper.ExecuteScalar($"SELECT COUNT(*) FROM Bookmarks WHERE UserId = {userId} AND BookId = {bookId}");
            if (result != null && Convert.ToInt32(result) == 0) // Если закладки ещё нет
                // INSERT INTO Bookmarks — добавляем новую закладку
                DatabaseHelper.ExecuteNonQuery($"INSERT INTO Bookmarks (UserId, BookId) VALUES ({userId}, {bookId})");
        }

        public void RemoveBookmark(int userId, int bookId) // Удалить книгу из закладок
        {
            // DELETE FROM Bookmarks WHERE UserId = ... AND BookId = ... — удаляем конкретную закладку
            DatabaseHelper.ExecuteNonQuery($"DELETE FROM Bookmarks WHERE UserId = {userId} AND BookId = {bookId}");
        }

        public void AddReview(int userId, int bookId, int rating, string comment) // Добавить отзыв
        {
            // INSERT INTO Reviews (UserId, BookId, Rating, Comment) VALUES (...) — добавляем отзыв
            string query = $@"INSERT INTO Reviews (UserId, BookId, Rating, Comment) 
                             VALUES ({userId}, {bookId}, {rating}, N'{comment.Replace("'", "''")}')";
            DatabaseHelper.ExecuteNonQuery(query);
            var bookService = new BookService();
            bookService.UpdateBookRating(bookId);        // Пересчитываем средний рейтинг книги
        }
    }
}