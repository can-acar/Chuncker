namespace Chuncker.Interfaces
{
    /// <summary>
    /// Middleware sıralaması için interface
    /// </summary>
    public interface IOrderedMiddleware
    {
        /// <summary>
        /// Middleware'in sıralamasını belirten sayı (düşük sayı = önce çalışır)
        /// </summary>
        int Order { get; }
    }

    /// <summary>
    /// Middleware sıralaması için attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class MiddlewareOrderAttribute : Attribute
    {
        /// <summary>
        /// Middleware'in sıralamasını belirten sayı (düşük sayı = önce çalışır)
        /// </summary>
        public int Order { get; }

        /// <summary>
        /// Middleware sıralaması için attribute
        /// </summary>
        /// <param name="order">Sıralama değeri (düşük sayı = önce çalışır)</param>
        public MiddlewareOrderAttribute(int order)
        {
            Order = order;
        }
    }

    /// <summary>
    /// Standart middleware sıralama değerleri
    /// </summary>
    public static class MiddlewareOrder
    {
        /// <summary>
        /// Authentication middleware (en önce çalışmalı)
        /// </summary>
        public const int Authentication = 10;

        /// <summary>
        /// Authorization middleware
        /// </summary>
        public const int Authorization = 20;

        /// <summary>
        /// Validation middleware (iş mantığından önce)
        /// </summary>
        public const int Validation = 30;

        /// <summary>
        /// Logging middleware (her şeyi loglamalı)
        /// </summary>
        public const int Logging = 40;

        /// <summary>
        /// Performance monitoring middleware
        /// </summary>
        public const int Performance = 50;

        /// <summary>
        /// Caching middleware
        /// </summary>
        public const int Caching = 60;

        /// <summary>
        /// Transaction middleware
        /// </summary>
        public const int Transaction = 70;

        /// <summary>
        /// Error handling middleware
        /// </summary>
        public const int ErrorHandling = 80;

        /// <summary>
        /// Custom business logic middleware
        /// </summary>
        public const int Business = 90;

        /// <summary>
        /// Default order for middleware without explicit ordering
        /// </summary>
        public const int Default = 100;
    }
}