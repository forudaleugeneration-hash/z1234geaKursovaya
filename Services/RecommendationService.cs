using System.Data;                             // DataTable, DataRow — работа с результатами SQL-запросов
using LibraryApp.Database;                     // DatabaseHelper — выполнение запросов к базе данных
using LibraryApp.Models;                       // Book — модель данных книги

namespace LibraryApp.Services
{
    public class RecommendationService         // Сервис рекомендаций — возвращает последние добавленные книги
    {
        private readonly BookService _bookService; // Сервис книг — нужен для MapBook() (DataRow → Book)

        public RecommendationService()          // Конструктор: создаёт BookService при создании сервиса
        {
            _bookService = new BookService();   // new — выделяет память под новый экземпляр BookService
        }

        public List<Book> GetPersonalRecommendations(int userId) // Персональные рекомендации (упрощено: последние добавленные)
        {
            return GetRecentlyAddedBooks(10);    // Просто возвращаем 10 последних добавленных книг
        }

        public List<Book> GetRecentlyAddedBooks(int limit = 10) // Получить N последних добавленных книг; limit=10 — по умолчанию
        {
            var books = new List<Book>();        // Пустой список для результата
            // TOP {limit} — ограничение количества (например, TOP 10)
            // ORDER BY b.DateAdded DESC — сортировка: новые сверху (DateAdded — дата добавления)
            // INNER JOIN Authors — подтягивает имя автора по AuthorId
            // INNER JOIN Genres — подтягивает название жанра по GenreId
            string query = $@"SELECT TOP {limit} b.*, a.AuthorName, g.GenreName 
                             FROM Books b 
                             INNER JOIN Authors a ON b.AuthorId = a.AuthorId 
                             INNER JOIN Genres g ON b.GenreId = g.GenreId 
                             ORDER BY b.DateAdded DESC";

            var dt = DatabaseHelper.ExecuteQuery(query); // Выполняем SQL, получаем DataTable
            foreach (DataRow row in dt.Rows)             // Перебираем строки результата
            {
                books.Add(_bookService.MapBook(row));    // DataRow → Book через MapBook, добавляем в список
            }
            return books;                                // Возвращаем готовый список
        }
        public List<Book> GetTrendingBooks()    // Трендовые книги (упрощено: последние добавленные)
        {
            return GetRecentlyAddedBooks(10);    // Возвращаем 10 последних добавленных книг
        }
    }
}