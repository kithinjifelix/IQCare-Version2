using System;
using System.Threading;
using System.Threading.Tasks;
using IQCare.Common.BusinessProcess.Services;
using IQCare.Common.Core.Models;
using IQCare.Common.Infrastructure;
using IQCare.HTS.BusinessProcess.Commands;
using IQCare.HTS.BusinessProcess.Services;
using IQCare.HTS.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IQCare.HTS.BusinessProcess.CommandHandlers
{
    public class SynchronizeFamilyCommandHandler : IRequestHandler<SynchronizeFamilyCommand, Result<SynchronizeFamilyResponse>>
    {
        private readonly ICommonUnitOfWork _unitOfWork;
        private readonly IHTSUnitOfWork _htsUnitOfWork;

        public SynchronizeFamilyCommandHandler(ICommonUnitOfWork commonUnitOfWork, IHTSUnitOfWork htsUnitOfWork)
        {
            _unitOfWork = commonUnitOfWork ?? throw new ArgumentNullException(nameof(commonUnitOfWork));
            _htsUnitOfWork = htsUnitOfWork ?? throw new ArgumentNullException(nameof(htsUnitOfWork));
        }

        public async Task<Result<SynchronizeFamilyResponse>> Handle(SynchronizeFamilyCommand request, CancellationToken cancellationToken)
        {
            using (_htsUnitOfWork)
            using (_unitOfWork)
            {
                try
                {
                    RegisterPersonService registerPersonService = new RegisterPersonService(_unitOfWork);
                    EncounterTestingService encounterTestingService = new EncounterTestingService(_unitOfWork, _htsUnitOfWork);

                    var facilityId = request.MESSAGE_HEADER.SENDING_FACILITY;

                    for (int i = 0; i < request.FAMILY.Count; i++)
                    {
                        string afyaMobileId = string.Empty;
                        string indexClientAfyaMobileId = string.Empty;

                        for (int j = 0; j < request.FAMILY[i].PATIENT_IDENTIFICATION.INTERNAL_PATIENT_ID.Count; j++)
                        {
                            if (request.FAMILY[i].PATIENT_IDENTIFICATION.INTERNAL_PATIENT_ID[j].IDENTIFIER_TYPE ==
                                "AFYA_MOBILE_ID")
                            {
                                afyaMobileId = request.FAMILY[i].PATIENT_IDENTIFICATION.INTERNAL_PATIENT_ID[j].ID;
                            }

                            if (request.FAMILY[i].PATIENT_IDENTIFICATION.INTERNAL_PATIENT_ID[j].IDENTIFIER_TYPE ==
                                "INDEX_CLIENT_AFYAMOBILE_ID")
                            {
                                indexClientAfyaMobileId =
                                    request.FAMILY[i].PATIENT_IDENTIFICATION.INTERNAL_PATIENT_ID[j].ID;
                            }
                        }

                        string firstName = request.FAMILY[i].PATIENT_IDENTIFICATION.PATIENT_NAME.FIRST_NAME;
                        string middleName = request.FAMILY[i].PATIENT_IDENTIFICATION.PATIENT_NAME.MIDDLE_NAME;
                        string lastName = request.FAMILY[i].PATIENT_IDENTIFICATION.PATIENT_NAME.LAST_NAME;
                        int sex = request.FAMILY[i].PATIENT_IDENTIFICATION.SEX;
                        DateTime dateOfBirth = DateTime.ParseExact(request.FAMILY[i].PATIENT_IDENTIFICATION.DATE_OF_BIRTH, "yyyyMMdd", null);
                        int providerId = request.FAMILY[i].PATIENT_IDENTIFICATION.USER_ID;
                        int maritalStatusId = request.FAMILY[i].PATIENT_IDENTIFICATION.MARITAL_STATUS;
                        string mobileNumber = request.FAMILY[i].PATIENT_IDENTIFICATION.PHONE_NUMBER;
                        string landmark = request.FAMILY[i].PATIENT_IDENTIFICATION.PATIENT_ADDRESS.PHYSICAL_ADDRESS
                            .LANDMARK;
                        int relationshipType = request.FAMILY[i].PATIENT_IDENTIFICATION.RELATIONSHIP_TYPE;

                        var indexClientIdentifiers = await registerPersonService.getPersonIdentifiers(indexClientAfyaMobileId, 10);
                        if (indexClientIdentifiers.Count > 0)
                        {
                            //Get Index client
                            var indexClient =
                                await registerPersonService.GetPatientByPersonId(indexClientIdentifiers[0].PersonId);
                            //Register family
                            var person = await registerPersonService.RegisterPerson(firstName, middleName, lastName, sex, dateOfBirth, providerId);
                            //Add afyamobile Id as an Id of the family
                            var personIdentifier = await registerPersonService.addPersonIdentifiers(person.Id, 10, afyaMobileId, providerId);
                            //Add family marital status
                            var partnerMaritalStatus = await registerPersonService.AddMaritalStatus(person.Id, maritalStatusId, providerId);
                            //add family contacts
                            var partnerContacts = await registerPersonService.addPersonContact(person.Id, null,
                                mobileNumber, null, null, providerId);
                            //add family location
                            var partnerLocation =
                                await registerPersonService.addPersonLocation(person.Id, 0, 0, 0, null, landmark,
                                    providerId);
                            //Add PersonRelationship
                            var personRelationship = await registerPersonService.addPersonRelationship(person.Id, indexClient.Id, relationshipType, providerId);


                            /***
                             *Encounter
                             */

                            
                            var lookupitem = await _unitOfWork.Repository<LookupItemView>()
                                .Get(x => x.MasterName == "TracingType" && x.ItemName == "Family").ToListAsync();
                            int tracingType = lookupitem[0].ItemId;

                            //encounterTestingService.addTracing(person.Id, tracingType, )



                        }
                    }

                    return Result<SynchronizeFamilyResponse>.Valid(new SynchronizeFamilyResponse()
                    {
                        
                    });
                }
                catch (Exception e)
                {
                    return Result<SynchronizeFamilyResponse>.Invalid(e.Message);
                }
            }
        }
    }
}