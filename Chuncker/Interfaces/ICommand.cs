namespace Chuncker.Interfaces
{
    /// <summary>
    /// Temel komut arayüzü (CQRS deseni)
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// İzleme için komut korelasyon kimliği
        /// </summary>
        Guid CorrelationId { get; set; }
    }

    /// <summary>
    /// Dönüş değeri olan komut
    /// </summary>
    /// <typeparam name="TResult">Dönüş türü</typeparam>
    public interface ICommand<TResult> : ICommand
    {
    }

    /// <summary>
    /// Komut işleyici arayüzü
    /// </summary>
    /// <typeparam name="TCommand">Komut türü</typeparam>
    public interface ICommandHandler<in TCommand> where TCommand : ICommand
    {
        /// <summary>
        /// Komutu işler
        /// </summary>
        /// <param name="command">İşlenecek komut</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Görev</returns>
        Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Dönüş değeri olan komut işleyici
    /// </summary>
    /// <typeparam name="TCommand">Komut türü</typeparam>
    /// <typeparam name="TResult">Dönüş türü</typeparam>
    public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
    {
        /// <summary>
        /// Komutu işler ve sonucu döndürür
        /// </summary>
        /// <param name="command">İşlenecek komut</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Komut sonucu</returns>
        Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Sorumluluk Zinciri için komut ara katman arayüzü
    /// </summary>
    /// <typeparam name="TCommand">Komut türü</typeparam>
    public interface ICommandMiddleware<TCommand> where TCommand : ICommand
    {
        /// <summary>
        /// Komutu ara katman üzerinden işler
        /// </summary>
        /// <param name="command">İşlenecek komut</param>
        /// <param name="next">Zincirdeki bir sonraki ara katman</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Görev</returns>
        Task HandleAsync(TCommand command, Func<Task> next, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Dönüş değeri olan komut ara katmanı
    /// </summary>
    /// <typeparam name="TCommand">Komut türü</typeparam>
    /// <typeparam name="TResult">Dönüş türü</typeparam>
    public interface ICommandMiddleware<TCommand, TResult> where TCommand : ICommand<TResult>
    {
        /// <summary>
        /// Komutu ara katman üzerinden işler ve sonucu döndürür
        /// </summary>
        /// <param name="command">İşlenecek komut</param>
        /// <param name="next">Zincirdeki bir sonraki ara katman</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Komut sonucu</returns>
        Task<TResult> HandleAsync(TCommand command, Func<Task<TResult>> next, CancellationToken cancellationToken = default);
    }
}