using Dapper;
using log4net;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
/// <summary>
/// Summary description for IntegrationProcess
/// </summary>
public class IntegrationProcess : Process
{
    private readonly ILog _Logger = LogManager.GetLogger(typeof(IntegrationProcess));
    private static EmailHandler _EmailHandler = null;
    private static SMSHandler _SMSHandler = null;
    private static readonly object emaillock = new object();
    private static readonly object smslock = new object();

    public IntegrationProcess()
    {
        
        EmailInstance();
        SMSInstance();
    }
    private void EmailInstance()
    {
        //single instance
        lock (emaillock)
        {
            if (_EmailHandler == null)
            {
                _EmailHandler = new EmailHandler();
                _EmailHandler.EmailInstance();
            }
        }
    }
    private void SMSInstance()
    {
        lock (smslock)
        {
            //single instance
            if (_SMSHandler == null)
            {
                _SMSHandler = new SMSHandler();
                _SMSHandler.SMSInstance();
            }
        }
    }
    //Approve Partner
    //Approvers Update
    //approver registration   
    //partners registration        
    //Partners Update    
    public override IResponse IntegrationTransaction(MySqlConnection Connection, MySqlTransaction Transaction, IModel Model, RequestType RType)
    {
        
        switch (RType)
        {
            #region Approve Partner
            case RequestType.ApprovePartner:
                try
                {
                    //_username = partners username
                    Models.Approver data = (Models.Approver)Model;
                    Dictionary<string, object> isApprovePartnerParam = new Dictionary<string, object>();

                    int type = 0;

                    #region Disapprove Partner
                    if (data.isApproved.Equals(2))
                    {
                        isApprovePartnerParam.Add("_username", data.username);
                        isApprovePartnerParam.Add("_approver", data.approver);
                        isApprovePartnerParam.Add("_remarks", data.remarks.IsNull() ? "" : data.remarks);
                        isApprovePartnerParam.Add("_type", type);
                        isApprovePartnerParam.Add("_isApproved", data.isApproved);

                        int disApprovalResult = Connection.Execute(StoredProcedures.APPROVE_PARTNER, isApprovePartnerParam, Transaction, 60, CommandType.StoredProcedure);
                        if (disApprovalResult < 1)
                        {
                            _Logger.Error(string.Format("Disapprove Partner Failed: {0}", data.Serialize()));
                            Transaction.Rollback();
                            return new Response { ResponseCode = 400, ResponsMessage = "Unable to process request. Please try again later." };
                        }
                        Transaction.Commit();
                        #region SMS Notification
                        //
                        //send sms notification to partner regarding the request
                        //
                        List<dynamic> approvers_contact = Connection.Query<dynamic>(StoredProcedures.GET_APPROVERS_CONTACT, new { _type = 0 }, null, false, 40, CommandType.StoredProcedure).ToList();

                        Thread executePartnerSMS = new Thread(delegate ()
                        {
                            _SMSHandler.SmsNotification(data.contact_number,
                                string.Format("Hi {0} {1} {2} {3} {4} {5} {6} {7} {8}", data.business_name,
                                                "We regret to inform you that your recent application for being MLhuillier partner has been denied.",
                                                "After a thorough review of your application and the supporting documents you supplied,",
                                                "we have determined that it would not be possible to accommodate your request at this time.",
                                                "Your application was denied because of the following reason(s):", data.remarks,
                                                "For inquiries, please e - mail us at ", approvers_contact[0].email, "and", approvers_contact[0].tg_email));
                        })
                        {
                            IsBackground = true
                        };
                        executePartnerSMS.Start();
                        #endregion

                        #region email notification
                        //
                        //Send email notification to partner as a confirmation that the request was denied
                        //
                        string transdate = Connection.Query<string>(StoredProcedures.GET_DATE, new { _type = 0, _username = data.username }, null, false, 40, CommandType.StoredProcedure).FirstOrDefault();
                        var mBody = new StringBuilder();
                        mBody.Append("<html><head></head><body><br/><font face='arial' align='left' size='2px'><p class='normal' style='text-indent: 0px;text-align:left'>" +
                           "Hi " + data.business_name + "," +
                           "<br/><br/>Registration Date and Time: " + transdate +
                           "<br/>Registered Business Name:" + data.business_name +
                           "<br/><br/><br/>Thank you for registering as MLhuillier partner!" +
                           "<br/><br/>We regret to inform you that your recent application for being MLhuillier partner has been denied." +
                           "<br/After a thorough review of your application and the supporting documents you supplied, we have" +
                           "<b/>determined that it would not be possible to accommodate your request at this time." +
                           "<b/><b/>Your application was denied because of the following reason(s):<br/>" + data.remarks +
                           "<b/><b/>For inquiries, please e-mail us at " + approvers_contact[0].email + " and " + approvers_contact[0].tg_email + "." +
                           "<b/><b/>Thank you again for your interest and for applying" +
                           "<b/><b/><b/>At your service," +
                           "M Lhuillier Financial Service, Inc.</p></font></p></font></body></html>");
                        Thread executePartnerEmail = new Thread(delegate ()
                        {
                            _EmailHandler.SendEmail(data.email, mBody.ToString());
                        })
                        {
                            IsBackground = true
                        };
                        executePartnerEmail.Start();
                        #endregion

                        _Logger.Info(string.Format("Disapprove Partner successfull: {0}", data.Serialize()));
                        return new Response { ResponseCode = 200, ResponsMessage = "Partner was successfully disapproved." };
                    }
                    #endregion

                    #region Approve Partner
                    if (data.division.Equals(DivisionType.FSD))
                    {
                        int checkFSDLevel = Connection.Query<int>(StoredProcedures.CHECK_FSD_LEVEL, new { _username = data.username }, null, false, 40, CommandType.StoredProcedure).FirstOrDefault();
                        if (checkFSDLevel == 0)
                        {
                            type = 1;
                            #region SMS Notification
                            //
                            //send sms notification to partner regarding the request
                            //
                            List<dynamic> FSD_contact = Connection.Query<dynamic>(StoredProcedures.GET_APPROVERS_CONTACT, new { _type = 0 }, null, false, 40, CommandType.StoredProcedure).ToList();

                            Thread executePartnerSMS = new Thread(delegate ()
                            {
                                _SMSHandler.SmsNotification(data.contact_number,
                                    string.Format("Hi {0}, {1} {2} {3} {4} {5} {6} {7} {8}", data.business_name,
                                                    "Thank you for registering as MLhuillier partner!",
                                                    "We have received your application and will be submitted to our Financial Services Division for approval.",
                                                    "As application is on process, you will be notified thru your email and contact number.",
                                                    "Please visit " + Models.Email.Link + " to access our API for your test reference.",
                                                    "For inquiries, please e - mail us at ", FSD_contact[0].email, "and", FSD_contact[0].tg_email));
                            })
                            {
                                IsBackground = true
                            };
                            executePartnerSMS.Start();
                            //
                            //send sms notification to FSD regarding the request
                            //
                            Thread executeDivisionSMS = new Thread(delegate ()
                            {
                                _SMSHandler.SmsNotification(FSD_contact[0].contact_number,
                                                             string.Format("A new partner {0} {1} {2} {3} {4}", data.business_name,
                                                                           "has been registered and waiting for your approval.",
                                                                           "Please visit URL", Models.Email.Link, "to administer the partner registration request."));
                            })
                            {
                                IsBackground = true
                            };
                            executeDivisionSMS.Start();
                            #endregion

                            #region email notification
                            //
                            //Send email notification to partner as a confirmation that the request was successful
                            //
                            string transdate = Connection.Query<string>(StoredProcedures.GET_DATE, new { _type = 0, username = string.Empty }, null, false, 40, CommandType.StoredProcedure).FirstOrDefault();
                            var mBody = new StringBuilder();
                            mBody.Append("<html><head></head><body><br/><font face='arial' align='left' size='2px'><p class='normal' style='text-indent: 0px;text-align:left'>" +
                               "Hi " + data.business_name + "," +
                               "<br/><br/>Registration Date and Time: " + transdate +
                               "<br/>Registered Business Name:" + data.business_name +
                               "<br/><br/><br/>Thank you for registering as MLhuillier partner!" +
                               "<br/><br/>We have received your application and will be submitted to our Financial Services Division for approval." +
                               "<br/You can access our API for your test reference by clicking the URL below:<b/>" + Models.Email.Link +
                               "<b/><b/>As application is on process, you will be notified thru your email " + data.email + " and contact number " + data.contact_number + "." +
                               "<b/><b/>For inquiries, please e-mail us at " + FSD_contact[0].email + " and " + FSD_contact[0].tg_email + "." +
                               "<b/><b/>Please ensure that your User ID and password are CONFIDENTIAL at all times." +
                               "<b/><b/><b/>At your service," +
                               "M Lhuillier Financial Service, Inc.</p></font></p></font></body></html>");
                            Thread executePartnerEmail = new Thread(delegate ()
                            {
                                _EmailHandler.SendEmail(data.email, mBody.ToString());
                            })
                            {
                                IsBackground = true
                            };
                            executePartnerEmail.Start();
                            #endregion

                        }
                        else
                        {
                            type = 4;
                            #region SMS Notification
                            //
                            //send sms notification to partner regarding the request
                            //
                            List<dynamic> FSD_contact = Connection.Query<dynamic>(StoredProcedures.GET_APPROVERS_CONTACT, new { _type = 0 }, null, false, 40, CommandType.StoredProcedure).ToList();

                            Thread executePartnerSMS = new Thread(delegate ()
                            {
                                _SMSHandler.SmsNotification(data.contact_number,
                                    string.Format("Hi {0}, {1} {2} {3} {4} {5} {6} {7} {8} {9}", data.business_name, "Congratulations!",
                                                    "Your registration as MLhuillier partner is now APPROVED from our company president Sir Michael A. Lhuillier.",
                                                    "It is now submitted to our Financial Services Division for checking of documents.",
                                                    "As application is on process, you will be notified thru your email and contact number.",
                                                    "Please visit " + Models.Email.Link + " to access our API for your test reference.",
                                                    "For inquiries, please e - mail us at ", FSD_contact[0].email, "and", FSD_contact[0].tg_email));
                            })
                            {
                                IsBackground = true
                            };
                            executePartnerSMS.Start();
                            //
                            //send sms notification to FSD regarding the request
                            //
                            Thread executeDivisionSMS = new Thread(delegate ()
                            {
                                _SMSHandler.SmsNotification(FSD_contact[0].contact_number,
                                                             string.Format("A new partner {0} {1} {2} {3} {4}", data.business_name,
                                                                           "has been registered and waiting for your approval.",
                                                                           "Please visit URL", Models.Email.Link, "to administer the partner registration request."));
                            })
                            {
                                IsBackground = true
                            };
                            executeDivisionSMS.Start();
                            #endregion

                            #region email notification
                            //
                            //Send email notification to partner as a confirmation that the request was successful
                            //
                            string transdate = Connection.Query<string>(StoredProcedures.GET_DATE, new { _type = 0, username = string.Empty }, null, false, 40, CommandType.StoredProcedure).FirstOrDefault();
                            var mBody = new StringBuilder();
                            mBody.Append("<html><head></head><body><br/><font face='arial' align='left' size='2px'><p class='normal' style='text-indent: 0px;text-align:left'>" +
                               "Hi " + data.business_name + "," +
                               "<br/><br/>Congratulations!" +
                               "<br/><br/>Your registration as MLhuillier partner is now APPROVED from our company president Sir Michael A. Lhuillier." +
                               "<br/><br/>It is now submitted to our Financial Services Division for checking of documents." +
                               "<br/You can access our API for your test reference by clicking the URL below:<b/>" + Models.Email.Link +
                               "<b/><b/>As application is on process, you will be notified thru your email " + data.email + " and contact number " + data.contact_number + "." +
                               "<b/><b/>For inquiries, please e-mail us at " + FSD_contact[0].email + " and " + FSD_contact[0].tg_email + "." +
                               "<b/><b/>Please ensure that your User ID and password are CONFIDENTIAL at all times." +
                               "<b/><b/><b/>At your service," +
                               "M Lhuillier Financial Service, Inc.</p></font></p></font></body></html>");
                            Thread executePartnerEmail = new Thread(delegate ()
                            {
                                _EmailHandler.SendEmail(data.email, mBody.ToString());
                            })
                            {
                                IsBackground = true
                            };
                            executePartnerEmail.Start();
                            #endregion
                        }
                    }
                    else if (data.division.Equals(DivisionType.SECCOM))
                    {
                        type = 2;
                        #region SMS Notification
                        //
                        //send sms notification to partner regarding the request
                        //
                        List<dynamic> FSD_contact = Connection.Query<dynamic>(StoredProcedures.GET_APPROVERS_CONTACT, new { _type = 0 }, null, false, 40, CommandType.StoredProcedure).ToList();

                        Thread executePartnerSMS = new Thread(delegate()
                        {
                            _SMSHandler.SmsNotification(data.contact_number,
                                string.Format("Hi {0}, {1} {2} {3} {4} {5} {6} {7} {8} {9}", data.business_name,"Congratulations!",
                                                "Your registration as MLhuillier partner is now APPROVED from our Financial Services Division.",
                                                "It is now submitted to our Security and Compliance (SECCOM) Division for approval.",
                                                "As application is on process, you will be notified thru your email and contact number.",
                                                "Please visit " + Models.Email.Link + " to access our API for your test reference.",
                                                "For inquiries, please e - mail us at ", FSD_contact[0].email, "and", FSD_contact[0].tg_email));
                        })
                        {
                            IsBackground = true
                        };
                        executePartnerSMS.Start();
                        //
                        //send sms notification to FSD regarding the request
                        //
                        Thread executeDivisionSMS = new Thread(delegate()
                        {
                            _SMSHandler.SmsNotification(FSD_contact[0].contact_number,
                                                         string.Format("A new partner {0} {1} {2} {3} {4}", data.business_name,
                                                                       "has been registered and waiting for your approval.",
                                                                       "Please visit URL", Models.Email.Link, "to administer the partner registration request."));
                        })
                        {
                            IsBackground = true
                        };
                        executeDivisionSMS.Start();
                        #endregion

                        #region email notification
                        //
                        //Send email notification to partner as a confirmation that the request was successful
                        //
                        string transdate = Connection.Query<string>(StoredProcedures.GET_DATE, new { _type = 0, username = string.Empty }, null, false, 40, CommandType.StoredProcedure).FirstOrDefault();
                        var mBody = new StringBuilder();
                        mBody.Append("<html><head></head><body><br/><font face='arial' align='left' size='2px'><p class='normal' style='text-indent: 0px;text-align:left'>" +
                           "Hi " + data.business_name + "," +
                           "<br/><br/>Congratulations!" +
                           "<br/><br/>Your registration as MLhuillier partner is now APPROVED from our Financial Services Division." +
                           "<br/><br/>It is now submitted to our Security and Compliance (SECCOM) Division for approval." +
                           "<br/You can access our API for your test reference by clicking the URL below:<b/>" + Models.Email.Link +
                           "<b/><b/>As application is on process, you will be notified thru your email " + data.email + " and contact number " + data.contact_number + "." +
                           "<b/><b/>For inquiries, please e-mail us at " + FSD_contact[0].email + " and " + FSD_contact[0].tg_email + "." +
                           "<b/><b/>Please ensure that your User ID and password are CONFIDENTIAL at all times." +
                           "<b/><b/><b/>At your service," +
                           "M Lhuillier Financial Service, Inc.</p></font></p></font></body></html>");
                        Thread executePartnerEmail = new Thread(delegate()
                        {
                            _EmailHandler.SendEmail(data.email, mBody.ToString());
                        })
                        {
                            IsBackground = true
                        };
                        executePartnerEmail.Start();
                        #endregion

                    }
                    else if (data.division.Equals(DivisionType.CEO))
                    {
                        type = 3;
                        #region SMS Notification
                        //
                        //send sms notification to partner regarding the request
                        //
                        List<dynamic> FSD_contact = Connection.Query<dynamic>(StoredProcedures.GET_APPROVERS_CONTACT, new { _type = 0 }, null, false, 40, CommandType.StoredProcedure).ToList();

                        Thread executePartnerSMS = new Thread(delegate ()
                        {
                            _SMSHandler.SmsNotification(data.contact_number,
                                string.Format("Hi {0}, {1} {2} {3} {4} {5} {6} {7} {8} {9}", data.business_name, "Congratulations!",
                                                "Your registration as MLhuillier partner is now APPROVED from our Security and Compliance (SECCOM) Division.",
                                                "It is now submitted to our company president Sir Michael A. Lhuillier for approval.",
                                                "As application is on process, you will be notified thru your email and contact number.",
                                                "Please visit " + Models.Email.Link + " to access our API for your test reference.",
                                                "For inquiries, please e - mail us at ", FSD_contact[0].email, "and", FSD_contact[0].tg_email));
                        })
                        {
                            IsBackground = true
                        };
                        executePartnerSMS.Start();
                        //
                        //send sms notification to FSD regarding the request
                        //
                        Thread executeDivisionSMS = new Thread(delegate ()
                        {
                            _SMSHandler.SmsNotification(FSD_contact[0].contact_number,
                                                         string.Format("A new partner {0} {1} {2} {3} {4}", data.business_name,
                                                                       "has been registered and waiting for your approval.",
                                                                       "Please visit URL", Models.Email.Link, "to administer the partner registration request."));
                        })
                        {
                            IsBackground = true
                        };
                        executeDivisionSMS.Start();
                        #endregion

                        #region email notification
                        //
                        //Send email notification to partner as a confirmation that the request was successful
                        //
                        string transdate = Connection.Query<string>(StoredProcedures.GET_DATE, new { _type = 0, username = string.Empty }, null, false, 40, CommandType.StoredProcedure).FirstOrDefault();
                        var mBody = new StringBuilder();
                        mBody.Append("<html><head></head><body><br/><font face='arial' align='left' size='2px'><p class='normal' style='text-indent: 0px;text-align:left'>" +
                           "Hi " + data.business_name + "," +
                           "<br/><br/>Congratulations!" +
                           "<br/><br/>Your registration as MLhuillier partner is now APPROVED from our Security and Compliance (SECCOM) Division." +
                           "<br/><br/>It is now submitted to our company president Sir Michael A. Lhuillier for approval." +
                           "<br/You can access our API for your test reference by clicking the URL below:<b/>" + Models.Email.Link +
                           "<b/><b/>As application is on process, you will be notified thru your email " + data.email + " and contact number " + data.contact_number + "." +
                           "<b/><b/>For inquiries, please e-mail us at " + FSD_contact[0].email + " and " + FSD_contact[0].tg_email + "." +
                           "<b/><b/>Please ensure that your User ID and password are CONFIDENTIAL at all times." +
                           "<b/><b/><b/>At your service," +
                           "M Lhuillier Financial Service, Inc.</p></font></p></font></body></html>");
                        Thread executePartnerEmail = new Thread(delegate ()
                        {
                            _EmailHandler.SendEmail(data.email, mBody.ToString());
                        })
                        {
                            IsBackground = true
                        };
                        executePartnerEmail.Start();
                        #endregion
                    }
                    else if (data.division.Equals(DivisionType.CAD))
                    {
                        type = 5;
                        #region SMS Notification
                        //
                        //send sms notification to partner regarding the request
                        //
                        List<dynamic> FSD_contact = Connection.Query<dynamic>(StoredProcedures.GET_APPROVERS_CONTACT, new { _type = 0 }, null, false, 40, CommandType.StoredProcedure).ToList();

                        Thread executePartnerSMS = new Thread(delegate ()
                        {
                            _SMSHandler.SmsNotification(data.contact_number,
                                string.Format("Hi {0}, {1} {2} {3} {4} {5} {6} {7} {8} {9}", data.business_name, "Congratulations!",
                                                "Your registration as MLhuillier partner is now APPROVED from our Financial Services Division.",
                                                "It is now submitted to our Central Accounting Division for approval.",
                                                "As application is on process, you will be notified thru your email and contact number.",
                                                "Please visit " + Models.Email.Link + " to access our API for your test reference.",
                                                "For inquiries, please e - mail us at ", FSD_contact[0].email, "and", FSD_contact[0].tg_email));
                        })
                        {
                            IsBackground = true
                        };
                        executePartnerSMS.Start();
                        //
                        //send sms notification to FSD regarding the request
                        //
                        Thread executeDivisionSMS = new Thread(delegate ()
                        {
                            _SMSHandler.SmsNotification(FSD_contact[0].contact_number,
                                                         string.Format("A new partner {0} {1} {2} {3} {4}", data.business_name,
                                                                       "has been registered and waiting for your approval.",
                                                                       "Please visit URL", Models.Email.Link, "to administer the partner registration request."));
                        })
                        {
                            IsBackground = true
                        };
                        executeDivisionSMS.Start();
                        #endregion

                        #region email notification
                        //
                        //Send email notification to partner as a confirmation that the request was successful
                        //
                        string transdate = Connection.Query<string>(StoredProcedures.GET_DATE, new { _type = 0, username = string.Empty }, null, false, 40, CommandType.StoredProcedure).FirstOrDefault();
                        var mBody = new StringBuilder();
                        mBody.Append("<html><head></head><body><br/><font face='arial' align='left' size='2px'><p class='normal' style='text-indent: 0px;text-align:left'>" +
                           "Hi " + data.business_name + "," +
                           "<br/><br/>Congratulations!" +
                           "<br/><br/>Your registration as MLhuillier partner is now APPROVED from our Financial Services Division." +
                           "<br/><br/>It is now submitted to our Central Accounting Division for approval." +
                           "<br/You can access our API for your test reference by clicking the URL below:<b/>" + Models.Email.Link +
                           "<b/><b/>As application is on process, you will be notified thru your email " + data.email + " and contact number " + data.contact_number + "." +
                           "<b/><b/>For inquiries, please e-mail us at " + FSD_contact[0].email + " and " + FSD_contact[0].tg_email + "." +
                           "<b/><b/>Please ensure that your User ID and password are CONFIDENTIAL at all times." +
                           "<b/><b/><b/>At your service," +
                           "M Lhuillier Financial Service, Inc.</p></font></p></font></body></html>");
                        Thread executePartnerEmail = new Thread(delegate ()
                        {
                            _EmailHandler.SendEmail(data.email, mBody.ToString());
                        })
                        {
                            IsBackground = true
                        };
                        executePartnerEmail.Start();
                        #endregion
                    }
                    else if (data.division.Equals(DivisionType.TG_HELPDESK))
                    {
                        int checkHDLevel = Connection.Query<int>(StoredProcedures.CHECK_HELPDESK_LEVEL, new { _username = data.username }, null, false, 40, CommandType.StoredProcedure).FirstOrDefault();
                        if (checkHDLevel == 0)
                        {
                            type = 6;
                            #region SMS Notification
                            //
                            //send sms notification to partner regarding the request
                            //
                            List<dynamic> FSD_contact = Connection.Query<dynamic>(StoredProcedures.GET_APPROVERS_CONTACT, new { _type = 0 }, null, false, 40, CommandType.StoredProcedure).ToList();

                            Thread executePartnerSMS = new Thread(delegate ()
                            {
                                _SMSHandler.SmsNotification(data.contact_number,
                                    string.Format("Hi {0}, {1} {2} {3} {4} {5} {6} {7} {8} {9}", data.business_name, "Congratulations!",
                                                    "Your registration as MLhuillier partner is now APPROVED from our Central Accounting Division.",
                                                    "It is now submitted to our Helpdesk for checking of requirements and creation of request for integration.",
                                                    "As application is on process, you will be notified thru your email and contact number.",
                                                    "Please visit " + Models.Email.Link + " to access our API for your test reference.",
                                                    "For inquiries, please e - mail us at ", FSD_contact[0].email, "and", FSD_contact[0].tg_email));
                            })
                            {
                                IsBackground = true
                            };
                            executePartnerSMS.Start();
                            //
                            //send sms notification to FSD regarding the request
                            //
                            Thread executeDivisionSMS = new Thread(delegate ()
                            {
                                _SMSHandler.SmsNotification(FSD_contact[0].contact_number,
                                                             string.Format("A new partner {0} {1} {2} {3} {4}", data.business_name,
                                                                           "has been registered and waiting for your approval.",
                                                                           "Please visit URL", Models.Email.Link, "to administer the partner registration request."));
                            })
                            {
                                IsBackground = true
                            };
                            executeDivisionSMS.Start();
                            #endregion

                            #region email notification
                            //
                            //Send email notification to partner as a confirmation that the request was successful
                            //
                            string transdate = Connection.Query<string>(StoredProcedures.GET_DATE, new { _type = 0, username = string.Empty }, null, false, 40, CommandType.StoredProcedure).FirstOrDefault();
                            var mBody = new StringBuilder();
                            mBody.Append("<html><head></head><body><br/><font face='arial' align='left' size='2px'><p class='normal' style='text-indent: 0px;text-align:left'>" +
                               "Hi " + data.business_name + "," +
                               "<br/><br/>Congratulations!" +
                               "<br/><br/>Your registration as MLhuillier partner is now APPROVED from our Central Accounting Division." +
                               "<br/><br/>It is now submitted to our Helpdesk for checking of requirements and creation of request for integration." +
                               "<br/You can access our API for your test reference by clicking the URL below:<b/>" + Models.Email.Link +
                               "<b/><b/>As application is on process, you will be notified thru your email " + data.email + " and contact number " + data.contact_number + "." +
                               "<b/><b/>For inquiries, please e-mail us at " + FSD_contact[0].email + " and " + FSD_contact[0].tg_email + "." +
                               "<b/><b/>Please ensure that your User ID and password are CONFIDENTIAL at all times." +
                               "<b/><b/><b/>At your service," +
                               "M Lhuillier Financial Service, Inc.</p></font></p></font></body></html>");
                            Thread executePartnerEmail = new Thread(delegate ()
                            {
                                _EmailHandler.SendEmail(data.email, mBody.ToString());
                            })
                            {
                                IsBackground = true
                            };
                            executePartnerEmail.Start();
                            #endregion
                        }
                        else
                        {
                            type = 8;
                            #region SMS Notification
                            //
                            //send sms notification to partner regarding the request
                            //
                            List<dynamic> FSD_contact = Connection.Query<dynamic>(StoredProcedures.GET_APPROVERS_CONTACT, new { _type = 0 }, null, false, 40, CommandType.StoredProcedure).ToList();

                            Thread executePartnerSMS = new Thread(delegate ()
                            {
                                _SMSHandler.SmsNotification(data.contact_number,
                                    string.Format("Hi {0}, {1} {2} {3} {4} {5} {6} {7} {8} {9}", data.business_name, "Congratulations!",
                                                    "Your registration as MLhuillier partner is now APPROVED from our Tech Group Assistant CTO.",
                                                    "It is now submitted to our Helpdesk for submission of request for integration development.",
                                                    "As application is on process, you will be notified thru your email and contact number.",
                                                    "Please visit " + Models.Email.Link + " to access our API for your test reference.",
                                                    "For inquiries, please e - mail us at ", FSD_contact[0].email, "and", FSD_contact[0].tg_email));
                            })
                            {
                                IsBackground = true
                            };
                            executePartnerSMS.Start();
                            //
                            //send sms notification to FSD regarding the request
                            //
                            Thread executeDivisionSMS = new Thread(delegate ()
                            {
                                _SMSHandler.SmsNotification(FSD_contact[0].contact_number,
                                                             string.Format("A new partner {0} {1} {2} {3} {4}", data.business_name,
                                                                           "has been registered and waiting for your approval.",
                                                                           "Please visit URL", Models.Email.Link, "to administer the partner registration request."));
                            })
                            {
                                IsBackground = true
                            };
                            executeDivisionSMS.Start();
                            #endregion

                            #region email notification
                            //
                            //Send email notification to partner as a confirmation that the request was successful
                            //
                            string transdate = Connection.Query<string>(StoredProcedures.GET_DATE, new { _type = 0, username = string.Empty }, null, false, 40, CommandType.StoredProcedure).FirstOrDefault();
                            var mBody = new StringBuilder();
                            mBody.Append("<html><head></head><body><br/><font face='arial' align='left' size='2px'><p class='normal' style='text-indent: 0px;text-align:left'>" +
                               "Hi " + data.business_name + "," +
                               "<br/><br/>Congratulations!" +
                               "<br/><br/>Your registration as MLhuillier partner is now APPROVED from our Tech Group Assistant CTO." +
                               "<br/><br/>It is now submitted to our Helpdesk for submission of request for integration development." +
                               "<br/You can access our API for your test reference by clicking the URL below:<b/>" + Models.Email.Link +
                               "<b/><b/>As application is on process, you will be notified thru your email " + data.email + " and contact number " + data.contact_number + "." +
                               "<b/><b/>For inquiries, please e-mail us at " + FSD_contact[0].email + " and " + FSD_contact[0].tg_email + "." +
                               "<b/><b/>Please ensure that your User ID and password are CONFIDENTIAL at all times." +
                               "<b/><b/><b/>At your service," +
                               "M Lhuillier Financial Service, Inc.</p></font></p></font></body></html>");
                            Thread executePartnerEmail = new Thread(delegate ()
                            {
                                _EmailHandler.SendEmail(data.email, mBody.ToString());
                            })
                            {
                                IsBackground = true
                            };
                            executePartnerEmail.Start();
                            #endregion
                        }
                    }
                    else if (data.division.Equals(DivisionType.TG_ASST_CTO))
                    {
                        type = 7;
                        #region SMS Notification
                        //
                        //send sms notification to partner regarding the request
                        //
                        List<dynamic> FSD_contact = Connection.Query<dynamic>(StoredProcedures.GET_APPROVERS_CONTACT, new { _type = 0 }, null, false, 40, CommandType.StoredProcedure).ToList();

                        Thread executePartnerSMS = new Thread(delegate ()
                        {
                            _SMSHandler.SmsNotification(data.contact_number,
                                string.Format("Hi {0}, {1} {2} {3} {4} {5} {6} {7} {8} {9}", data.business_name, "Congratulations!",
                                                "Your registration as MLhuillier partner is now APPROVED from our Helpdesk.",
                                                "It is now submitted to our Tech Group Assistant CTO for approval of request for integration.",
                                                "As application is on process, you will be notified thru your email and contact number.",
                                                "Please visit " + Models.Email.Link + " to access our API for your test reference.",
                                                "For inquiries, please e - mail us at ", FSD_contact[0].email, "and", FSD_contact[0].tg_email));
                        })
                        {
                            IsBackground = true
                        };
                        executePartnerSMS.Start();
                        //
                        //send sms notification to FSD regarding the request
                        //
                        Thread executeDivisionSMS = new Thread(delegate ()
                        {
                            _SMSHandler.SmsNotification(FSD_contact[0].contact_number,
                                                         string.Format("A new partner {0} {1} {2} {3} {4}", data.business_name,
                                                                       "has been registered and waiting for your approval.",
                                                                       "Please visit URL", Models.Email.Link, "to administer the partner registration request."));
                        })
                        {
                            IsBackground = true
                        };
                        executeDivisionSMS.Start();
                        #endregion

                        #region email notification
                        //
                        //Send email notification to partner as a confirmation that the request was successful
                        //
                        string transdate = Connection.Query<string>(StoredProcedures.GET_DATE, new { _type = 0, username = string.Empty }, null, false, 40, CommandType.StoredProcedure).FirstOrDefault();
                        var mBody = new StringBuilder();
                        mBody.Append("<html><head></head><body><br/><font face='arial' align='left' size='2px'><p class='normal' style='text-indent: 0px;text-align:left'>" +
                           "Hi " + data.business_name + "," +
                           "<br/><br/>Congratulations!" +
                           "<br/><br/>Your registration as MLhuillier partner is now APPROVED from our Helpdesk." +
                           "<br/><br/>It is now submitted to our Tech Group Assistant CTO for approval of request for integration." +
                           "<br/You can access our API for your test reference by clicking the URL below:<b/>" + Models.Email.Link +
                           "<b/><b/>As application is on process, you will be notified thru your email " + data.email + " and contact number " + data.contact_number + "." +
                           "<b/><b/>For inquiries, please e-mail us at " + FSD_contact[0].email + " and " + FSD_contact[0].tg_email + "." +
                           "<b/><b/>Please ensure that your User ID and password are CONFIDENTIAL at all times." +
                           "<b/><b/><b/>At your service," +
                           "M Lhuillier Financial Service, Inc.</p></font></p></font></body></html>");
                        Thread executePartnerEmail = new Thread(delegate ()
                        {
                            _EmailHandler.SendEmail(data.email, mBody.ToString());
                        })
                        {
                            IsBackground = true
                        };
                        executePartnerEmail.Start();
                        #endregion
                    }
                    else if (data.division.Equals(DivisionType.TG_PRO))
                    {
                        type = 9;
                        #region SMS Notification
                        //
                        //send sms notification to partner regarding the request
                        //
                        List<dynamic> FSD_contact = Connection.Query<dynamic>(StoredProcedures.GET_APPROVERS_CONTACT, new { _type = 0 }, null, false, 40, CommandType.StoredProcedure).ToList();

                        Thread executePartnerSMS = new Thread(delegate ()
                        {
                            _SMSHandler.SmsNotification(data.contact_number,
                                string.Format("Hi {0}, {1} {2} {3} {4} {5} {6} {7} {8} {9}", data.business_name, "Congratulations!",
                                                "Your registration as MLhuillier partner is now APPROVED from our Helpdesk.",
                                                "It is now submitted to our Tech Group Partners Relation Office for Hi/Hello meeting.",
                                                "As application is on process, you will be notified thru your email and contact number.",
                                                "Please visit " + Models.Email.Link + " to access our API for your test reference.",
                                                "For inquiries, please e - mail us at ", FSD_contact[0].email, "and", FSD_contact[0].tg_email));
                        })
                        {
                            IsBackground = true
                        };
                        executePartnerSMS.Start();
                        //
                        //send sms notification to FSD regarding the request
                        //
                        Thread executeDivisionSMS = new Thread(delegate ()
                        {
                            _SMSHandler.SmsNotification(FSD_contact[0].contact_number,
                                                         string.Format("A new partner {0} {1} {2} {3} {4}", data.business_name,
                                                                       "has been registered and waiting for your approval.",
                                                                       "Please visit URL", Models.Email.Link, "to administer the partner registration request."));
                        })
                        {
                            IsBackground = true
                        };
                        executeDivisionSMS.Start();
                        #endregion

                        #region email notification
                        //
                        //Send email notification to partner as a confirmation that the request was successful
                        //
                        string transdate = Connection.Query<string>(StoredProcedures.GET_DATE, new { _type = 0, username = string.Empty }, null, false, 40, CommandType.StoredProcedure).FirstOrDefault();
                        var mBody = new StringBuilder();
                        mBody.Append("<html><head></head><body><br/><font face='arial' align='left' size='2px'><p class='normal' style='text-indent: 0px;text-align:left'>" +
                           "Hi " + data.business_name + "," +
                           "<br/><br/>Congratulations!" +
                           "<br/><br/>Your registration as MLhuillier partner is now APPROVED from our Helpdesk." +
                           "<br/><br/>It is now submitted to our Tech Group Partners Relation Office for Hi/Hello meeting." +
                           "<br/You can access our API for your test reference by clicking the URL below:<b/>" + Models.Email.Link +
                           "<b/><b/>As application is on process, you will be notified thru your email " + data.email + " and contact number " + data.contact_number + "." +
                           "<b/><b/>For inquiries, please e-mail us at " + FSD_contact[0].email + " and " + FSD_contact[0].tg_email + "." +
                           "<b/><b/>Please ensure that your User ID and password are CONFIDENTIAL at all times." +
                           "<b/><b/><b/>At your service," +
                           "M Lhuillier Financial Service, Inc.</p></font></p></font></body></html>");
                        Thread executePartnerEmail = new Thread(delegate ()
                        {
                            _EmailHandler.SendEmail(data.email, mBody.ToString());
                        })
                        {
                            IsBackground = true
                        };
                        executePartnerEmail.Start();
                        #endregion
                    }
                    else if (data.division.Equals(DivisionType.TG_PMO))
                    {
                        type = 10;
                        #region SMS Notification
                        //
                        //send sms notification to partner regarding the request
                        //
                        List<dynamic> FSD_contact = Connection.Query<dynamic>(StoredProcedures.GET_APPROVERS_CONTACT, new { _type = 0 }, null, false, 40, CommandType.StoredProcedure).ToList();

                        Thread executePartnerSMS = new Thread(delegate ()
                        {
                            _SMSHandler.SmsNotification(data.contact_number,
                                string.Format("Hi {0}, {1} {2} {3} {4} {5} {6} {7} {8} {9}", data.business_name, "Congratulations!",
                                                "Your registration as MLhuillier partner is now APPROVED from Tech Group Partners Relation Office .",
                                                "It is now submitted to our Tech Group Project Management Office for timeline and integration.",
                                                "As application is on process, you will be notified thru your email and contact number.",
                                                "Please visit " + Models.Email.Link + " to access our API for your test reference.",
                                                "For inquiries, please e - mail us at ", FSD_contact[0].email, "and", FSD_contact[0].tg_email));
                        })
                        {
                            IsBackground = true
                        };
                        executePartnerSMS.Start();
                        //
                        //send sms notification to FSD regarding the request
                        //
                        Thread executeDivisionSMS = new Thread(delegate ()
                        {
                            _SMSHandler.SmsNotification(FSD_contact[0].contact_number,
                                                         string.Format("A new partner {0} {1} {2} {3} {4}", data.business_name,
                                                                       "has been registered and waiting for your approval.",
                                                                       "Please visit URL", Models.Email.Link, "to administer the partner registration request."));
                        })
                        {
                            IsBackground = true
                        };
                        executeDivisionSMS.Start();
                        #endregion

                        #region email notification
                        //
                        //Send email notification to partner as a confirmation that the request was successful
                        //
                        string transdate = Connection.Query<string>(StoredProcedures.GET_DATE, new { _type = 0, username = string.Empty }, null, false, 40, CommandType.StoredProcedure).FirstOrDefault();
                        var mBody = new StringBuilder();
                        mBody.Append("<html><head></head><body><br/><font face='arial' align='left' size='2px'><p class='normal' style='text-indent: 0px;text-align:left'>" +
                           "Hi " + data.business_name + "," +
                           "<br/><br/>Congratulations!" +
                           "<br/><br/>Your registration as MLhuillier partner is now APPROVED from Tech Group Partners Relation Office ." +
                           "<br/><br/>It is now submitted to our Tech Group Project Management Office for timeline and integration." +
                           "<br/You can access our API for your test reference by clicking the URL below:<b/>" + Models.Email.Link +
                           "<b/><b/>As application is on process, you will be notified thru your email " + data.email + " and contact number " + data.contact_number + "." +
                           "<b/><b/>For inquiries, please e-mail us at " + FSD_contact[0].email + " and " + FSD_contact[0].tg_email + "." +
                           "<b/><b/>Please ensure that your User ID and password are CONFIDENTIAL at all times." +
                           "<b/><b/><b/>At your service," +
                           "M Lhuillier Financial Service, Inc.</p></font></p></font></body></html>");
                        Thread executePartnerEmail = new Thread(delegate ()
                        {
                            _EmailHandler.SendEmail(data.email, mBody.ToString());
                        })
                        {
                            IsBackground = true
                        };
                        executePartnerEmail.Start();
                        #endregion
                    }
                    isApprovePartnerParam.Add("_username", data.username);
                    isApprovePartnerParam.Add("_approver", data.approver);
                    isApprovePartnerParam.Add("_remarks", data.remarks.IsNull() ? "" : data.remarks);
                    isApprovePartnerParam.Add("_isApproved", data.isApproved);
                    isApprovePartnerParam.Add("_type", type);

                    int approvalResult = Connection.Execute(StoredProcedures.APPROVE_PARTNER, isApprovePartnerParam, Transaction, 60, CommandType.StoredProcedure);
                    if (approvalResult < 1)
                    {
                        _Logger.Error(string.Format("Approve Partner Failed: {0}", data.Serialize()));
                        Transaction.Rollback();
                        return new Response { ResponseCode = 400, ResponsMessage = "Unable to process request. Please try again later." };
                    }
                    #endregion


                    Transaction.Commit();
                    _Logger.Info(string.Format("Approve Partner successfull: {0}", data.Serialize()));
                    return new Response { ResponseCode = 200, ResponsMessage = "Partner was successfully approved. Thank you." };
                }
                catch (MySqlException mex)
                {
                    Transaction.Rollback();
                    _Logger.Error(string.Format("Approve Partner Transaction Failed: {0}", Model.Serialize()));
                    _Logger.Fatal(mex.ToString());
                    return new Response { ResponseCode = 400, ResponsMessage = "Unable to process request. Please check your data and try again." };
                }
                catch (TimeoutException tex)
                {
                    Transaction.Rollback();
                    _Logger.Error(string.Format("Approve Partner Transaction Failed: {0}", Model.Serialize()));
                    _Logger.Fatal(tex.ToString());
                    return new Response { ResponseCode = 408, ResponsMessage = "Unable to process request. Connection timeout occured. Please try again later." };
                }
                catch (Exception ex)
                {
                    Transaction.Rollback();
                    _Logger.Error(string.Format("Approve Partner Transaction Failed: {0}", Model.Serialize()));
                    _Logger.Fatal(ex.ToString());
                    return new Response { ResponseCode = 500, ResponsMessage = "Unable to process request. The system encountered some technical problem. Sorry for the inconvenience." };
                }
            #endregion

            #region  Approvers Update

            case RequestType.ApproversUpdate:
                try
                {
                    Models.Admin data = (Models.Admin)Model;
                    Dictionary<string, object> ApproverUpdateParam = new Dictionary<string, object>()
                                {
                                    {"_username", data.username},
                                    {"_password", data.password},
                                    {"_firstname",data.firstname},
                                    {"_middlename", data.middlename},
                                    {"_lastname", data.lastname},
                                    {"_division", data.division},
                                    {"_level", data.level},
                                    {"_operator_id", data.operator_id},
                                    {"_isActive", data.isActive},
                                    {"_contact_number", data.contact_number},
                                    {"_email", data.email},
                                };
                    int updateResult = Connection.Execute(StoredProcedures.APPROVERS_UPDATE, ApproverUpdateParam, Transaction, 60, CommandType.StoredProcedure);
                    if (updateResult < 1)
                    {
                        _Logger.Error(string.Format("Approver Update Failed: {0}", data.Serialize()));
                        Transaction.Rollback();
                        return new Response { ResponseCode = 400, ResponsMessage = "Unable to process request. Please try again later." };
                    }

                    //EmailHandler email = new EmailHandler();

                    //Thread execute = new Thread(delegate()
                    //{
                    //    email.SendEmail("email" + "|", "partners name", "series", 1);
                    //});
                    //execute.IsBackground = true;
                    //execute.Start();
                    Transaction.Commit();
                    _Logger.Info(string.Format("Approver Update successfull: {0}", data.Serialize()));
                    return new Response { ResponseCode = 200, ResponsMessage = string.Format("{0} user {1} {2} {3} was successfully updated. Thank you.", data.level, data.firstname, data.middlename, data.lastname) };
                }
                catch (MySqlException mex)
                {
                    Transaction.Rollback();
                    _Logger.Error(string.Format("Approver Update Transaction Failed: {0}", Model.Serialize()));
                    _Logger.Fatal(mex.ToString());
                    return new Response { ResponseCode = 400, ResponsMessage = "Unable to process request. Please check your data and try again." };
                }
                catch (TimeoutException tex)
                {
                    Transaction.Rollback();
                    _Logger.Error(string.Format("Approver Update Transaction Failed: {0}", Model.Serialize()));
                    _Logger.Fatal(tex.ToString());
                    return new Response { ResponseCode = 408, ResponsMessage = "Unable to process request. Connection timeout occured. Please try again later." };
                }
                catch (Exception ex)
                {
                    Transaction.Rollback();
                    _Logger.Error(string.Format("Approver Update Transaction Failed: {0}", Model.Serialize()));
                    _Logger.Fatal(ex.ToString());
                    return new Response { ResponseCode = 500, ResponsMessage = "Unable to process request. The system encountered some technical problem. Sorry for the inconvenience." };
                }
            #endregion

            #region Approvers Registration

            case RequestType.ApproverRegistration:
                try
                {
                    Models.Admin data = (Models.Admin)Model;
                    Dictionary<string, object> ApproverRegistrationParam = new Dictionary<string, object>()
                                {
                                    {"_username", data.username},
                                    {"_password", data.password},
                                    {"_id_number", data.id_number},
                                    {"_firstname",data.firstname},
                                    {"_middlename", data.middlename},
                                    {"_lastname", data.lastname},
                                    {"_division", data.division},
                                    {"_level", data.level},
                                    {"_operator_id", data.operator_id},
                                    {"_contact_number", data.contact_number},
                                    {"_email", data.email},
                                };
                    int registrationResult = Connection.Execute(StoredProcedures.APPROVER_REGISTRATION, ApproverRegistrationParam, Transaction, 60, CommandType.StoredProcedure);
                    if (registrationResult < 1)
                    {
                        _Logger.Error(string.Format("Approver Registration Failed: {0}", data.Serialize()));
                        Transaction.Rollback();
                        return new Response { ResponseCode = 400, ResponsMessage = "Unable to process request. Please try again later." };
                    }

                    //EmailHandler email = new EmailHandler();

                    //Thread execute = new Thread(delegate()
                    //{
                    //    email.SendEmail("email" + "|", "partners name", "series", 1);
                    //});
                    //execute.IsBackground = true;
                    //execute.Start();
                    Transaction.Commit();
                    _Logger.Info(string.Format("Approver Registration successfull: {0}", data.Serialize()));
                    return new Response { ResponseCode = 200, ResponsMessage = string.Format("{0} user {1} {2} {3} was successfully added as new approver. Thank you.", data.level, data.firstname, data.middlename, data.lastname) };
                }
                catch (MySqlException mex)
                {
                    Transaction.Rollback();
                    _Logger.Error(string.Format("Approver Registration Transaction Failed: {0}", Model.Serialize()));
                    _Logger.Fatal(mex.ToString());
                    return new Response { ResponseCode = 400, ResponsMessage = "Unable to process request. Please check your data and try again." };
                }
                catch (TimeoutException tex)
                {
                    Transaction.Rollback();
                    _Logger.Error(string.Format("Approver Registration Transaction Failed: {0}", Model.Serialize()));
                    _Logger.Fatal(tex.ToString());
                    return new Response { ResponseCode = 408, ResponsMessage = "Unable to process request. Connection timeout occured. Please try again later." };
                }
                catch (Exception ex)
                {
                    Transaction.Rollback();
                    _Logger.Error(string.Format("Approver Registration Transaction Failed: {0}", Model.Serialize()));
                    _Logger.Fatal(ex.ToString());
                    return new Response { ResponseCode = 500, ResponsMessage = "Unable to process request. The system encountered some technical problem. Sorry for the inconvenience." };
                }
            #endregion

            #region Partners Registration
            case RequestType.PartnersRegistration:
                try
                {
                    Models.PartnersData data = (Models.PartnersData)Model;

                    //check username if exist
                    int checkIfExist = Connection.Query<int>(StoredProcedures.IS_USERNAME_TAKEN, new { _username = data.username }, null, false, 40, CommandType.StoredProcedure).FirstOrDefault();
                    if (checkIfExist > 0)
                    {
                        Transaction.Rollback();
                        return new Response { ResponseCode = 401, ResponsMessage = "Username provided was already taken." };
                    }
                    Dictionary<string, object> RegistrationParam = new Dictionary<string, object>()
                      {
                        {"_username", data.username},
                        {"_password", data.password},
                        {"_business_name", data.business_name},
                        {"_email",data.email},
                        {"_contact_person", data.contact_person},
                        {"_contact_number", data.contact_number},
                        {"_memorandom_agreement", data.memorandom_agreement.IsNull() ? "" : data.memorandom_agreement},
                        {"_nondisclosure_agreement", data.nondisclosure_agreement.IsNull() ? "" : data.nondisclosure_agreement},
                        {"_registration_checklist", data.registration_checklist.IsNull() ? "" :  data.registration_checklist},
                        {"_access_form", data.access_form.IsNull() ? "" : data.access_form},
                        {"_technical_requirements", data.technical_requirements.IsNull() ? "" : data.technical_requirements},
                        {"_api_document", data.api_document.IsNull() ? "" : data.api_document},
                        {"_attachment_1", data.attachment_1.IsNull() ? "" : data.attachment_1},
                        {"_attachment_2",data.attachment_2.IsNull() ? "" : data.attachment_2},
                        {"_attachment_3", data.attachment_3.IsNull() ? "" : data.attachment_3},
                        {"_attachment_4", data.attachment_4.IsNull() ? "" : data.attachment_4},
                        {"_attachment_5", data.attachment_5.IsNull() ? "" : data.attachment_5},
                        {"_attachment_6", data.attachment_6.IsNull() ? "" : data.attachment_6},
                        {"_attachment_7", data.attachment_7.IsNull() ? "" : data.attachment_7},
                        {"_attachment_8",data.attachment_8.IsNull() ? "" : data.attachment_8},
                        {"_attachment_9", data.attachment_9.IsNull() ? "" : data.attachment_9},
                        {"_attachment_10",data.attachment_10.IsNull() ? "" : data.attachment_10},
                      };
                    int registrationResult = Connection.Execute(StoredProcedures.PARTNERS_REGISTRATION, RegistrationParam, Transaction, 60, CommandType.StoredProcedure);
                    if (registrationResult < 1)
                    {
                        _Logger.Error(string.Format("Partners Registration Failed: {0}", data.Serialize()));
                        Transaction.Rollback();
                        return new Response { ResponseCode = 400, ResponsMessage = "Unable to process request. Please try again later." };
                    }
                    Transaction.Commit();

                    #region SMS Notification
                    //
                    //send sms notification to partner regarding the request
                    //
                    List<dynamic> FSD_contact = Connection.Query<dynamic>(StoredProcedures.GET_APPROVERS_CONTACT, new { _type = 0 }, null, false, 40, CommandType.StoredProcedure).ToList();

                    Thread executePartnerSMS = new Thread(delegate ()
                    {
                        _SMSHandler.SmsNotification(data.contact_number,
                            string.Format("Hi {0} {1} {2} {3} {4} {5} {6} {7} {8}", data.business_name,
                                            "Thank you for registering as MLhuillier partner!",
                                            "We have received your application and will be submitted to our Financial Services Division for approval.",
                                            "As application is on process, you will be notified thru your email and contact number.",
                                            "Please visit "+Models.Email.Link+" to access our API for your test reference.",
                                            "For inquiries, please e - mail us at ", FSD_contact[0].email, "and", FSD_contact[0].tg_email));
                    })
                    {
                        IsBackground = true
                    };
                    executePartnerSMS.Start();
                    //
                    //send sms notification to FSD regarding the request
                    //
                    Thread executeDivisionSMS = new Thread(delegate ()
                    {
                        _SMSHandler.SmsNotification(FSD_contact[0].contact_number,
                                                     string.Format("A new partner {0} {1} {2} {3} {4}", data.business_name,
                                                                   "has been registered and waiting for your approval.",
                                                                   "Please visit URL", Models.Email.Link, "to administer the partner registration request."));
                    })
                    {
                        IsBackground = true
                    };
                    executeDivisionSMS.Start();
                    #endregion

                    #region email notification
                    //
                    //Send email notification to partner as a confirmation that the request was successful
                    //
                    string transdate = Connection.Query<string>(StoredProcedures.GET_DATE, new { _type = 0 }, null, false, 40, CommandType.StoredProcedure).FirstOrDefault();
                    var mBody = new StringBuilder();
                    mBody.Append("<html><head></head><body><br/><font face='arial' align='left' size='2px'><p class='normal' style='text-indent: 0px;text-align:left'>" +
                       "Hi " + data.business_name + "," +
                       "<br/><br/>Registration Date and Time: " + transdate +
                       "<br/>Registered Business Name:" + data.business_name +
                       "<br/><br/><br/>Thank you for registering as MLhuillier partner!" +
                       "<br/><br/>We have received your application and will be submitted to our Financial Services Division for approval." +
                       "<br/You can access our API for your test reference by clicking the URL below:<b/>" + Models.Email.Link +
                       "<b/><b/>As application is on process, you will be notified thru your email " + data.email + " and contact number " + data.contact_number + "." +
                       "<b/><b/>For inquiries, please e-mail us at " + FSD_contact[0].email + " and " + FSD_contact[0].tg_email + "." +
                       "<b/><b/>Please ensure that your User ID and password are CONFIDENTIAL at all times." +
                       "<b/><b/><b/>At your service," +
                       "M Lhuillier Financial Service, Inc.</p></font></p></font></body></html>");
                    Thread executePartnerEmail = new Thread(delegate ()
                    {
                        _EmailHandler.SendEmail(data.email, mBody.ToString());
                    })
                    {
                        IsBackground = true
                    };
                    executePartnerEmail.Start();
                    #endregion


                    _Logger.Info(string.Format("Partners Registration successfull: {0}", data.Serialize()));
                    return new Response { ResponseCode = 200, ResponsMessage = "Successfully submitted to FSD and wait for verification." };
                }
                catch (MySqlException mex)
                {
                    Transaction.Rollback();
                    _Logger.Error(string.Format("Partners Registration Transaction Failed: {0}", Model.Serialize()));
                    _Logger.Fatal(mex.ToString());
                    return new Response { ResponseCode = 400, ResponsMessage = "Unable to process request. Please check your data and try again." };
                }
                catch (TimeoutException tex)
                {
                    Transaction.Rollback();
                    _Logger.Error(string.Format("Partners Registration Transaction Failed: {0}", Model.Serialize()));
                    _Logger.Fatal(tex.ToString());
                    return new Response { ResponseCode = 408, ResponsMessage = "Unable to process request. Connection timeout occured. Please try again later." };
                }
                catch (Exception ex)
                {
                    Transaction.Rollback();
                    _Logger.Error(string.Format("Partners Registration Transaction Failed: {0}", Model.Serialize()));
                    _Logger.Fatal(ex.ToString());
                    return new Response { ResponseCode = 500, ResponsMessage = "Unable to process request. The system encountered some technical problem. Sorry for the inconvenience." };
                }
            #endregion

            #region Partners Update
            case RequestType.PartnersUpdate:
                try
                {
                    Models.PartnersData data = (Models.PartnersData)Model;
                    Dictionary<string, object> PartnersUpdateParam = new Dictionary<string, object>()
                        {
                         {"_username", data.username},
                         {"_password", data.password},
                         {"_attachment_1", data.attachment_1.IsNull() ? "" : data.attachment_1},
                         {"_attachment_2",data.attachment_2.IsNull() ? "" : data.attachment_2},
                         {"_attachment_3", data.attachment_3.IsNull() ? "" : data.attachment_3},
                         {"_attachment_4", data.attachment_4.IsNull() ? "" : data.attachment_4},
                         {"_attachment_5", data.attachment_5.IsNull() ? "" : data.attachment_5},
                         {"_attachment_6", data.attachment_6.IsNull() ? "" : data.attachment_6},
                         {"_attachment_7", data.attachment_7.IsNull() ? "" : data.attachment_7},
                         {"_attachment_8",data.attachment_8.IsNull() ? "" : data.attachment_8},
                         {"_attachment_9", data.attachment_9.IsNull() ? "" : data.attachment_9},
                         {"_attachment_10",data.attachment_10.IsNull() ? "" : data.attachment_10},
                        };

                    int updateResult = Connection.Execute(StoredProcedures.PARTNERS_UPDATE, PartnersUpdateParam, Transaction, 60, CommandType.StoredProcedure);
                    if (updateResult < 1)
                    {
                        _Logger.Error(string.Format("Partners Update Failed: {0}", data.Serialize()));
                        Transaction.Rollback();
                        return new Response { ResponseCode = 400, ResponsMessage = "Unable to process request. Please try again later." };
                    }
                    //EmailHandler email = new EmailHandler();

                    //Thread execute = new Thread(delegate()
                    //{
                    //    email.SendEmail("email" + "|", "partners name", "series", 1);
                    //});
                    //execute.IsBackground = true;
                    //execute.Start();
                    Transaction.Commit();
                    _Logger.Info(string.Format("Partners Update successfull: {0}", data.Serialize()));
                    return new Response { ResponseCode = 200, ResponsMessage = "Successfully resubmitted to FSD and wait for verification." };
                }
                catch (MySqlException mex)
                {
                    Transaction.Rollback();
                    _Logger.Error(string.Format("Partners Update Transaction Failed: {0}", Model.Serialize()));
                    _Logger.Fatal(mex.ToString());
                    return new Response { ResponseCode = 400, ResponsMessage = "Unable to process request. Please check your data and try again." };
                }
                catch (TimeoutException tex)
                {
                    Transaction.Rollback();
                    _Logger.Error(string.Format("Partners Update Transaction Failed: {0}", Model.Serialize()));
                    _Logger.Fatal(tex.ToString());
                    return new Response { ResponseCode = 408, ResponsMessage = "Unable to process request. Connection timeout occured. Please try again later." };
                }
                catch (Exception ex)
                {
                    Transaction.Rollback();
                    _Logger.Error(string.Format("Partners Update Transaction Failed: {0}", Model.Serialize()));
                    _Logger.Fatal(ex.ToString());
                    return new Response { ResponseCode = 500, ResponsMessage = "Unable to process request. The system encountered some technical problem. Sorry for the inconvenience." };
                }
            #endregion
            default:
                return new Response { ResponseCode = 404, ResponsMessage = "Unauthorized! Invalid method." };
        }
    }
    //admin login
    //partners login    
    public override IResponse IntegrationTransaction(MySqlConnection Connection, IModel Model, RequestType RType)
    {
        switch (RType)
        {
            #region Admin Login

            case RequestType.AdminLogin:
                try
                {
                    Models.Login data = (Models.Login)Model;
                    Models.Admin loginData = Connection.Query<Models.Admin>(StoredProcedures.ADMIN_LOGIN, new { _username = data.username, _password = data.password }, null, false, 60, CommandType.StoredProcedure).FirstOrDefault();
                    if (loginData == null)
                    {
                        _Logger.Info(string.Format("admin username: {0} password: {1}", data.username, data.password));
                        return new LoginResponse { ResponseCode = 404, ResponsMessage = "Invalid Credentials!" };
                    }
                    if (loginData.isActive == 0)
                    {
                        return new LoginResponse { ResponseCode = 404, ResponsMessage = "We're still processing your request. Thank you!" };
                    }

                    return new LoginResponse { ResponseCode = 200, ResponsMessage = "Success", adminData = loginData };
                }
                catch (MySqlException mex)
                {
                    _Logger.Fatal(mex.ToString());
                    return new LoginResponse { ResponseCode = 400, ResponsMessage = "Unable to process request. Please check your data and try again." };
                }
                catch (TimeoutException tex)
                {
                    _Logger.Fatal(tex.ToString());
                    return new LoginResponse { ResponseCode = 408, ResponsMessage = "Unable to process request. Connection timeout occured. Please try again later." };
                }
                catch (Exception ex)
                {
                    _Logger.Fatal(ex.ToString());
                    return new LoginResponse { ResponseCode = 500, ResponsMessage = "Unable to process request. The system encountered some technical problem. Sorry for the inconvenience." };
                }
            #endregion

            #region Partners Login
            case RequestType.PartnersLogin:
                try
                {
                    Models.Login data = (Models.Login)Model;
                    Models.PartnersData loginData = Connection.Query<Models.PartnersData>(StoredProcedures.PARTNERS_LOGIN, new { _username = data.username, _password = data.password }, null, false, 60, CommandType.StoredProcedure).FirstOrDefault();
                    if (loginData == null)
                    {
                        _Logger.Info(string.Format("username: {0} password: {1}", data.username, data.password));
                        return new LoginResponse { ResponseCode = 404, ResponsMessage = "Invalid Credentials!" };
                    }
                    if (loginData.isApproved == 0)
                    {
                        return new LoginResponse { ResponseCode = 404, ResponsMessage = "We're still processing your request. Thank you!" };
                    }
                    if (loginData.isApproved == 2)
                    {
                        return new LoginResponse { ResponseCode = 404, ResponsMessage = "Your request was disapproved!" };
                    }
                    return new LoginResponse { ResponseCode = 200, ResponsMessage = "Success", loginData = loginData };
                }
                catch (MySqlException mex)
                {
                    _Logger.Fatal(mex.ToString());
                    return new LoginResponse { ResponseCode = 400, ResponsMessage = "Unable to process request. Please check your data and try again." };
                }
                catch (TimeoutException tex)
                {
                    _Logger.Fatal(tex.ToString());
                    return new LoginResponse { ResponseCode = 408, ResponsMessage = "Unable to process request. Connection timeout occured. Please try again later." };
                }
                catch (Exception ex)
                {
                    _Logger.Fatal(ex.ToString());
                    return new LoginResponse { ResponseCode = 500, ResponsMessage = "Unable to process request. The system encountered some technical problem. Sorry for the inconvenience." };
                }
            #endregion

            default:
                return new Response { ResponseCode = 404, ResponsMessage = "Unauthorized! Invalid method." };
        }
    }
    //approver list
    //division list
    //partners list
    public override IResponse IntegrationTransaction(MySqlConnection Connection, RequestType Type)
    {
        switch (Type)
        {
            #region Approvers List

            case RequestType.ApproversList:
                try
                {

                    List<Models.Admin> approverList = Connection.Query<Models.Admin>(StoredProcedures.APPROVER_LIST, null, null, false, 60, CommandType.StoredProcedure).ToList();
                    if (approverList.Count < 1)
                    {
                        _Logger.Info("No data found!");
                        return new ListOfApproverResponse { ResponseCode = 404, ResponsMessage = "No data found." };
                    }

                    return new ListOfApproverResponse { ResponseCode = 200, ResponsMessage = "Success", adminList = approverList };
                }
                catch (MySqlException mex)
                {
                    _Logger.Fatal(mex.ToString());
                    return new ListOfApproverResponse { ResponseCode = 400, ResponsMessage = "Unable to process request. Please check your data and try again." };
                }
                catch (TimeoutException tex)
                {
                    _Logger.Fatal(tex.ToString());
                    return new ListOfApproverResponse { ResponseCode = 408, ResponsMessage = "Unable to process request. Connection timeout occured. Please try again later." };
                }
                catch (Exception ex)
                {
                    _Logger.Fatal(ex.ToString());
                    return new ListOfApproverResponse { ResponseCode = 500, ResponsMessage = "Unable to process request. The system encountered some technical problem. Sorry for the inconvenience." };
                }
            #endregion

            #region Division List

            case RequestType.DivisionList:
                try
                {

                    List<Models.Division> divisionList = Connection.Query<Models.Division>(StoredProcedures.DIVISION_LIST, null, null, false, 60, CommandType.StoredProcedure).ToList();
                    if (divisionList.Count < 1)
                    {
                        _Logger.Info("No data found!");
                        return new DivisionListResponse { ResponseCode = 404, ResponsMessage = "No data found." };
                    }

                    return new DivisionListResponse { ResponseCode = 200, ResponsMessage = "Success", divisionList = divisionList };
                }
                catch (MySqlException mex)
                {
                    _Logger.Fatal(mex.ToString());
                    return new DivisionListResponse { ResponseCode = 400, ResponsMessage = "Unable to process request. Please check your data and try again." };
                }
                catch (TimeoutException tex)
                {
                    _Logger.Fatal(tex.ToString());
                    return new DivisionListResponse { ResponseCode = 408, ResponsMessage = "Unable to process request. Connection timeout occured. Please try again later." };
                }
                catch (Exception ex)
                {
                    _Logger.Fatal(ex.ToString());
                    return new DivisionListResponse { ResponseCode = 500, ResponsMessage = "Unable to process request. The system encountered some technical problem. Sorry for the inconvenience." };
                }
            #endregion

            #region Partners List

            case RequestType.PartnersList:
                try
                {

                    List<Models.PartnersData> partnersList = Connection.Query<Models.PartnersData>(StoredProcedures.PARTNERS_LIST, null, null, false, 60, CommandType.StoredProcedure).ToList();
                    if (partnersList.Count < 1)
                    {
                        _Logger.Info("No data found!");
                        return new PartnersListResponse { ResponseCode = 404, ResponsMessage = "No data found." };
                    }

                    return new PartnersListResponse { ResponseCode = 200, ResponsMessage = "Success", partnersList = partnersList };
                }
                catch (MySqlException mex)
                {
                    _Logger.Fatal(mex.ToString());
                    return new PartnersListResponse { ResponseCode = 400, ResponsMessage = "Unable to process request. Please check your data and try again." };
                }
                catch (TimeoutException tex)
                {
                    _Logger.Fatal(tex.ToString());
                    return new PartnersListResponse { ResponseCode = 408, ResponsMessage = "Unable to process request. Connection timeout occured. Please try again later." };
                }
                catch (Exception ex)
                {
                    _Logger.Fatal(ex.ToString());
                    return new PartnersListResponse { ResponseCode = 500, ResponsMessage = "Unable to process request. The system encountered some technical problem. Sorry for the inconvenience." };
                }

            #endregion

            default:
                return new Response { ResponseCode = 404, ResponsMessage = "Unauthorized! Invalid method." };
        }
    }





}


