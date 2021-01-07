using System;
using System.Collections.Generic;
using Nop.Plugin.Payments.SafeTyPay.Domain;

namespace Nop.Plugin.Payments.SafeTyPay.Services
{
    public interface INotificationRequestService
    {
        /// <summary>
        /// Delete a notificationRequestTemp
        /// </summary>
        /// <param name="notificationRequestTemp"></param>
        void DeleteNotificationRequest(NotificationRequestSafeTyPay notificationRequestTemp);

        /// <summary>
        /// Insert a new notificationRequestTemp
        /// </summary>
        /// <param name="notificationRequestTemp"></param>
        void InsertNotificationRequest(NotificationRequestSafeTyPay notificationRequestTemp);

        /// <summary>
        /// Get a list of notificationRequestTemp
        /// </summary>
        /// <returns></returns>
        IList<NotificationRequestSafeTyPay> GetAllNotificationRequestTemp();

        /// <summary>
        /// Get a notificationRequestTemp by MerchanId
        /// </summary>
        /// <param name="merchandId"></param>
        /// <returns></returns>
        NotificationRequestSafeTyPay? GetNotificationRequestByMerchanId(Guid merchandId);

        /// <summary>
        /// Update a notificationRequestTemp
        /// </summary>
        /// <param name="notificationRequestTemp"></param>
        void UpdateNotificationRequest(NotificationRequestSafeTyPay notificationRequestTemp);
    }
}