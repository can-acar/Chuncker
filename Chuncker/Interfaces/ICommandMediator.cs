namespace Chuncker.Interfaces
{
    /// <summary>
    /// Komut aracı arayüzü (MediatR deseni)
    /// </summary>
    public interface ICommandMediator
    {
        /// <summary>
        /// İşleme için komut gönderir
        /// </summary>
        /// <typeparam name="TCommand">Komut türü</typeparam>
        /// <param name="command">Gönderilecek komut</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Görev</returns>
        Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) 
            where TCommand : ICommand;

        /// <summary>
        /// Komut gönderir ve sonuç alır
        /// </summary>
        /// <typeparam name="TCommand">Komut türü</typeparam>
        /// <typeparam name="TResult">Dönüş türü</typeparam>
        /// <param name="command">Gönderilecek komut</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Komut sonucu</returns>
        Task<TResult> SendAsync<TCommand, TResult>(TCommand command, CancellationToken cancellationToken = default) 
            where TCommand : ICommand<TResult>;
    }
}