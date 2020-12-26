using System;
using System.Collections.Generic;
using System.Linq;
using Nop.Data;
using Nop.Plugin.Payments.SafeTyPay.Domain;

namespace Nop.Plugin.Payments.SafeTyPay.Services
{
    public class NotificationRequestService : INotificationRequestService
    {
        #region Fields

        private readonly IRepository<NotificationRequestSafeTyPay> _repository;

        #endregion Fields

        #region Ctor

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="repository">NotificationRequestTemp</param>
        public NotificationRequestService(IRepository<NotificationRequestSafeTyPay> repository)
        {
            _repository = repository;
        }

        #endregion Ctor

        #region Methods

        public virtual void DeleteNotificationRequest(NotificationRequestSafeTyPay notificationRequestTemp)
        {
            _repository.Delete(notificationRequestTemp);
        }

        public virtual IList<NotificationRequestSafeTyPay> GetAllNotificationRequestTemp()
        {
            var query = _repository.Table;
            query = query.OrderByDescending(x => x.Id);
            var data = query.ToList();
            return data;
        }

        public virtual void InsertNotificationRequest(NotificationRequestSafeTyPay notificationRequestTemp)
        {
            if (notificationRequestTemp == null)
                throw new ArgumentNullException(nameof(notificationRequestTemp));

            _repository.Insert(notificationRequestTemp);
        }

        public virtual NotificationRequestSafeTyPay GetNotificationRequestByMerchanId(Guid merchandId)
        {
            if (merchandId == Guid.Empty)
                return null;

            var query = from o in _repository.Table
                        where o.MerchantSalesID == merchandId.ToString()
                        select o;
            var t = query.FirstOrDefault();
            return t;
        }

        public virtual void UpdateNotificationRequest(NotificationRequestSafeTyPay notificationRequestTemp)
        {
            if (notificationRequestTemp == null)
                throw new ArgumentNullException(nameof(notificationRequestTemp));

            _repository.Update(notificationRequestTemp);
        }

        #endregion Methods
    }
}