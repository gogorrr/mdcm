// mDCM: A C# DICOM library
//
// Copyright (c) 2006-2008  Colby Dillion
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Dicom.Data;

namespace Dicom.Network.Server
{
    public class QRService : DcmServiceBase
    {
        public DcmAssociationCallback OnAssociationRequest;

        public delegate DcmAssociateResult DcmAssociationCallback(QRService client, DcmAssociate association);
        public delegate DcmStatus DcmQRCFindCallback(QRService client, byte presentationID, ushort messageID, DcmPriority priority, DcmDataset dataset);
        public delegate DcmStatus DcmQRCMoveCallback(QRService client, byte presentationID, ushort messageID, string destinationAE, DcmPriority priority, DcmDataset dataset, out ushort remain, out ushort complete, out ushort warning, out ushort failure);
        
        public DcmQRCFindCallback OnCFindRequest;
        public DcmQRCMoveCallback OnCMoveRequest;

        public QRService() : base()
        {
            UseFileBuffer = false;
            LogID = "QR SCP";
        }


        public new void SendCFindResponse(byte presentationID, ushort messageIdRespondedTo, DcmDataset dataset, DcmStatus status)
        {
            base.SendCFindResponse(presentationID, messageIdRespondedTo, dataset, status);
        }


        protected override void OnReceiveAssociateRequest(DcmAssociate association)
        {
            association.NegotiateAsyncOps = false;
            LogID = association.CallingAE;
            if (OnAssociationRequest != null)
            {
                DcmAssociateResult result = OnAssociationRequest(this, association);
                if (result == DcmAssociateResult.RejectCalledAE)
                {
                    SendAssociateReject(DcmRejectResult.Permanent, DcmRejectSource.ServiceUser, DcmRejectReason.CalledAENotRecognized);
                    return;
                }
                else if (result == DcmAssociateResult.RejectCallingAE)
                {
                    SendAssociateReject(DcmRejectResult.Permanent, DcmRejectSource.ServiceUser, DcmRejectReason.CallingAENotRecognized);
                    return;
                }
                else if (result == DcmAssociateResult.RejectNoReason)
                {
                    SendAssociateReject(DcmRejectResult.Permanent, DcmRejectSource.ServiceUser, DcmRejectReason.NoReasonGiven);
                    return;
                }
                else
                {
                    foreach (DcmPresContext pc in association.GetPresentationContexts())
                    {
                        if (pc.Result == DcmPresContextResult.Proposed)
                            pc.SetResult(DcmPresContextResult.RejectNoReason);
                    }
                }
            }
            else
            {
                DcmAssociateProfile profile = DcmAssociateProfile.Find(association, true);
                profile.Apply(association);
            }

            SendAssociateAccept(association);
        }

        protected override void OnReceiveCEchoRequest(byte presentationID, ushort messageID, DcmPriority priority)
        {
            SendCEchoResponse(presentationID, messageID, DcmStatus.Success);
        }

        protected override void OnReceiveCFindRequest(byte presentationID, ushort messageID, DcmPriority priority, DcmDataset dataset)
        {
            DcmStatus status = DcmStatus.QueryRetrieveUnableToProcess;
            DcmDataset resultDataset = null;

            if (OnCFindRequest != null)
            {
                status = OnCFindRequest(this, presentationID, messageID, priority, dataset);
            }

            SendCFindResponse(presentationID, messageID, resultDataset, status);
        }

        protected override void OnReceiveCMoveRequest(byte presentationID, ushort messageID, string destinationAE, DcmPriority priority, DcmDataset dataset)
        {
            DcmStatus status = DcmStatus.QueryRetrieveUnableToProcess;
            ushort remain = 0;
            ushort complete = 0;
            ushort warning = 0;
            ushort failure = 0;

            if (OnCMoveRequest != null)
            {
                status = OnCMoveRequest(this, presentationID, messageID, destinationAE, priority, dataset, out remain, out complete, out warning, out failure);
            }

            SendCMoveResponse(presentationID, messageID, status, remain, complete, warning, failure);
        }
    }
}
