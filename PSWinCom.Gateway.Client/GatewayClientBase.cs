using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace PSWinCom.Gateway.Client
{
    public abstract class GatewayClientBase : IGatewayClient
    {
        public ITransport Transport { get; set; }

        public GatewayClientBase(ITransport transport)
        {
            Transport = transport;
        }

        public SendResult Send(params Message[] messages)
        {
            return Send(messages.AsEnumerable());
        }

        public virtual SendResult Send(IEnumerable<Message> messages)
        {
            var messageList = messages.ToList();
            var transportResult = Transport.Send(BuildPayload(messageList));
            return GetSendResult(messageList, transportResult);
        }

        protected XDocument BuildPayload(IEnumerable<Message> messages)
        {
            XDocument doc;
            doc = new XDocument(new XDeclaration("1.0", "iso-8859-1", null));
            doc.Add(
                new XDocumentType("SESSION", null, "pswincom_submit.dtd", null), 
                new XElement("SESSION", 
                    new XElement("CLIENT", Username), 
                    new XElement("PW", Password), 
                    new XElement("MSGLST", 
                        GetMessageElements(messages))));
            return doc;
        }

        private IEnumerable<XElement> GetMessageElements(IEnumerable<Message> messages)
        {
            var numInSession = 1;
            foreach (var msg in messages)
            {
                msg.NumInSession = numInSession++;
                XElement msgElement = new XElement("MSG", GetMessagePropertyElements(msg));
                yield return msgElement;
            }
        }

        private IEnumerable<XElement> GetMessagePropertyElements(Message msg)
        {
            var sms = msg as SmsMessage;
            var mms = msg as MmsMessage;

            yield return new XElement("ID", msg.NumInSession);
            yield return new XElement("TEXT", msg.Text);
            yield return new XElement("SND", msg.SenderNumber);
            yield return new XElement("RCV", msg.ReceiverNumber);

            if (!string.IsNullOrEmpty(msg.ShortCode))
                yield return new XElement("SHORTCODE", msg.ShortCode);

            if (msg.Tariff > 0)
                yield return new XElement("TARIFF", msg.Tariff);

            if (msg.RequestReceipt)
                yield return new XElement("RCPREQ", "Y");

            if (!string.IsNullOrEmpty(msg.CpaTag))
                yield return new XElement("CPATAG", msg.CpaTag);

            if (msg.Type.HasValue)
                yield return new XElement("OP", msg.Type.Value.ToString("D"));

            if (msg.TimeToLive.HasValue)
                yield return new XElement("TTL", msg.TimeToLive.Value.TotalMinutes.ToString("0"));

            if (msg.DeliveryTime.HasValue)
                yield return new XElement("DELIVERYTIME", msg.DeliveryTime.Value.ToString("yyyyMMddHHmm"));

            if (sms != null)
            {
                if (sms.Network != null)
                    yield return new XElement("NET", sms.Network.ToString());
                if (sms.AgeLimit.HasValue)
                    yield return new XElement("AGELIMIT", sms.AgeLimit.Value.ToString("0"));
                if (!string.IsNullOrEmpty(sms.ServiceCode))
                    yield return new XElement("SERVICECODE", sms.ServiceCode);
                if (sms.Replace.HasValue)
                    yield return new XElement("REPLACE", sms.Replace.Value.ToString("D"));
                if (sms != null && sms.IsFlashMessage)
                    yield return new XElement("CLASS", "0");
            }

            if (mms != null)
            {
                if (mms.MmsData != null && mms.MmsData.Length > 0)
                {
                    yield return new XElement("MMSFILE", System.Convert.ToBase64String(mms.MmsData));
                }
            }
        }

        protected static SendResult GetSendResult(IEnumerable<Message> messages, TransportResult transportResult)
        {
            var result = new SendResult();
            var userReferences = messages.ToDictionary((m) => m.NumInSession, m => m.UserReference);
            result.Results = transportResult
                .Content
                .Descendants("MSG")
                .Select((el) => new MessageResult { 
                    UserReference = userReferences[int.Parse(el.Element("ID").Value)], 
                    Status = el.Element("STATUS").Value, 
                    Message = el.Element("INFO") != null ? el.Element("INFO").Value : null
                });
            return result;
        }

        public string Password { get; set; }
        public string Username { get; set; }
    }
}