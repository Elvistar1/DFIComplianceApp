namespace DFIComplianceApp.Services;

public static class ExpertSystemLegalReferences
{
    public static readonly Dictionary<string, (string Section, string Risk, string Recommendation)> LegalReferences = new()
    {
        // Oil & Gas Stations
        ["Is the premises registered and the certificate displayed?"] = ("Section 3", "High",
    "Management must ensure the premises is officially registered with the appropriate authorities " +
    "as required under Section 3 of the Factories, Offices and Shops Act, 1970. The valid certificate " +
    "of registration should be displayed in a prominent and easily visible location on the premises at all times."),

        ["Are fire extinguishers adequate, serviced, and accessible?"] = ("Section 45", "High",
    "An adequate number of fire extinguishers must be installed, regularly inspected, and properly maintained. " +
    "Fire extinguishers must be strategically located throughout the premises, clearly visible, and easily accessible " +
    "to all staff in case of emergency."),

        ["Are all fuel dispensing equipment and tanks maintained and inspected regularly?"] = ("Section 48", "High",
    "All fuel dispensing equipment and storage tanks must undergo regular maintenance and safety inspections " +
    "to ensure proper functioning. Records of maintenance and inspection activities should be documented and kept up-to-date."),

        ["Are inflammable substances securely stored away from ignition sources?"] = ("Section 47", "High",
    "Inflammable substances must be securely stored in designated storage areas, away from potential ignition sources " +
    "such as open flames, electrical equipment, or heat-producing devices. Clear signage and appropriate safety measures " +
    "should be in place."),

        ["Is the premises clean and free from flammable waste?"] = ("Section 14", "Medium",
    "The premises must be maintained in a clean and orderly condition, ensuring that flammable waste and combustible materials " +
    "are regularly removed and properly disposed of. Regular housekeeping should be enforced to minimize fire risks."),

        ["Are safe escape routes and fire exits marked and unobstructed?"] = ("Section 45", "High",
    "All fire exits and escape routes must be clearly marked with appropriate signage and kept free from obstructions at all times. " +
    "Emergency lighting and exit indicators should also be installed to guide occupants during an evacuation."),

        ["Is there adequate ventilation around storage and dispensing areas?"] = ("Section 16", "Medium",
    "Adequate ventilation must be provided in storage and fuel dispensing areas to prevent the buildup of hazardous vapors. " +
    "Ventilation systems should be regularly checked and maintained to ensure effective operation."),

        ["Are emergency shut-off devices clearly marked and functional?"] = ("Section 48", "High",
    "Emergency shut-off devices for fuel dispensing equipment must be installed, clearly labeled, and kept in proper working condition. " +
    "All relevant staff should be trained on their location and use during emergency situations."),

        ["Is protective clothing provided for attendants?"] = ("Section 13", "Medium",
    "Appropriate protective clothing, including flame-resistant uniforms, gloves, and safety footwear, must be provided to all attendants " +
    "working with fuel dispensing operations. Usage of protective gear should be strictly enforced."),

        ["Are suitable arrangements made for confined space entry (tanks, pits)?"] = ("Section 49", "High",
    "Strict safety protocols must be established for confined space entry such as fuel tanks or pits. This includes gas testing, " +
    "permit-to-work systems, supervision, and provision of rescue equipment. Only trained personnel should be authorized to enter confined spaces."),

        ["Is adequate lighting provided for night operations?"] = ("Section 15", "Low",
    "Sufficient lighting must be provided in all operational areas, especially during night-time activities, to ensure safety and visibility " +
    "for workers and customers."),

        ["Are warning signs displayed (e.g. no smoking)?"] = ("Section 47", "High",
    "Clear and durable warning signs such as 'No Smoking,' 'Flammable Materials,' and 'Authorized Personnel Only' must be prominently displayed " +
    "throughout the premises, particularly near fuel storage and dispensing points."),

        ["Is a register of lifting tackle maintained (for overhead tanks/lifting gear)?"] = ("Section 33", "Medium",
    "A formal register documenting all lifting tackle, including overhead tanks and lifting gear, must be maintained. The register should record " +
    "testing, certification, and maintenance activities as per regulatory requirements."),

        ["Is there adequate and safe drinking water provided for staff?"] = ("Section 18", "Medium",
    "Management must provide clean, safe, and potable drinking water for all employees on the premises. Drinking facilities should be regularly cleaned and maintained."),

        ["Is a first-aid box available and up to standard?"] = ("Section 19", "High",
    "A well-stocked first-aid box, meeting the minimum required standards, must be available and easily accessible within the premises. " +
    "Contents should be checked regularly, and expired items replaced promptly."),

        // Food Processing Companies
        ["Is the premises clean, free from refuse and regularly sanitized?"] = ("Section 14", "Medium",
    "The premises must be maintained in a clean and hygienic condition at all times. Regular sanitation routines should be enforced, " +
    "and waste materials must be removed promptly to prevent contamination risks."),

        ["Are production areas free from overcrowding and well-ventilated?"] = ("Section 16", "Medium",
    "Production areas must be organized to avoid overcrowding, ensuring sufficient space for safe and efficient operations. " +
    "Adequate ventilation systems must be in place to maintain air quality and control humidity."),

        ["Are workers provided with appropriate protective clothing (aprons, gloves, head covers)?"] = ("Section 13", "Medium",
    "All food production workers must be provided with appropriate personal protective equipment, including aprons, gloves, and head covers. " +
    "This minimizes the risk of contamination and ensures worker safety."),

        ["Is equipment clean and regularly maintained?"] = ("Section 48", "Medium",
    "All food processing equipment must be thoroughly cleaned and sanitized regularly. A maintenance schedule should be established to ensure " +
    "all machines are in good working condition and free from contamination risks."),

        ["Are harmful fumes, dust or vapours effectively removed?"] = ("Section 17", "High",
    "Effective extraction systems must be installed to remove harmful fumes, dust, or vapors from the production environment. " +
    "This protects both product quality and employee health, as required under Section 17."),

        ["Is adequate washing and sanitary facilities provided for workers?"] = ("Section 18", "Medium",
    "Workers must have access to adequate washing facilities, including hand-washing stations, toilets, and changing rooms. " +
    "These facilities must be kept clean, stocked with necessary supplies, and segregated by gender where applicable."),

        ["Is food storage protected against contamination?"] = ("Section 14", "High",
    "Food storage areas must be properly enclosed, clean, and free from pest infestation. Products must be stored off the floor and away from walls " +
    "to facilitate cleaning and inspection, ensuring full compliance with food safety regulations."),

        ["Are food processing rooms properly ventilated and drained?"] = ("Section 16", "Medium",
    "All food processing rooms must be equipped with adequate ventilation to control temperature, moisture, and odor levels. " +
    "Proper drainage systems must also be installed and maintained to prevent standing water and contamination risks."),

        ["Is eating or drinking prohibited in production areas?"] = ("Section 17", "Low",
    "Strict policies must be enforced prohibiting eating or drinking within food production areas. This reduces the risk of contamination " +
    "and ensures compliance with occupational health and safety standards."),

        ["Are emergency exits functional and fire precautions enforced?"] = ("Section 45", "High",
    "Emergency exits must be clearly marked, unobstructed, and easily operable. Fire safety equipment such as extinguishers should be installed, " +
    "inspected regularly, and fire drills conducted periodically."),

        ["Is adequate lighting maintained in food preparation and storage areas?"] = ("Section 15", "Low",
    "Sufficient lighting must be provided in all food preparation and storage areas to ensure safety and hygiene standards. " +
    "Lighting fixtures should be maintained in good working order and shielded to prevent breakage into food products."),

        ["Are cleaning schedules and records maintained?"] = ("Section 14", "Medium",
    "A documented cleaning schedule must be implemented, specifying frequency, methods, and responsible personnel. " +
    "Records of all cleaning activities should be kept and reviewed regularly to ensure hygiene compliance."),

        ["Is a first-aid box available?"] = ("Section 19", "High",
    "A fully equipped first-aid box must be available in the food processing facility. The box should contain supplies suitable for common workplace injuries, " +
    "and its contents should be regularly checked and replenished."),

        ["Are accident records and occupational disease notifications up to date?"] = ("Section 50", "Medium",
    "Accident and occupational disease records must be accurately maintained and kept up to date. Any workplace injuries or illnesses must be reported " +
    "in accordance with regulatory requirements and promptly investigated."),


        // Wood Processing Companies
        ["Is the premises registered and general register maintained?"] = ("Section 3", "High",
    "The wood processing premises must be officially registered under the appropriate authority. " +
    "A general register must be maintained, recording all required legal details such as machinery, processes, and personnel."),

        ["Are all moving parts of saws, planers, and other machinery guarded?"] = ("Section 31", "High",
    "All machinery with moving parts must be fitted with secure and effective guards to prevent accidental contact. " +
    "Regular inspections should be conducted to ensure these safety devices remain functional."),

        ["Is dust extraction provided and effective at source?"] = ("Section 17", "High",
    "An efficient dust extraction system must be installed at all points where wood dust is generated. " +
    "The system should operate continuously during machine use to minimize inhalation risks and fire hazards."),

        ["Are emergency stop devices fitted on all machines?"] = ("Section 31", "High",
    "All machinery must be equipped with easily accessible emergency stop devices to quickly shut down equipment in the event of an emergency or malfunction. " +
    "These devices must be clearly labeled and tested regularly."),

        ["Is there adequate space between machines for safe operation?"] = ("Section 16", "Medium",
    "Sufficient clearance must be maintained between machines to allow safe movement of workers and materials. " +
    "Work areas should be laid out to prevent crowding and reduce the risk of accidents."),

        ["Are fire extinguishers available, accessible, and serviced?"] = ("Section 45", "High",
    "Appropriate fire extinguishers must be installed in easily accessible locations throughout the premises. " +
    "Extinguishers must be inspected, serviced, and replaced according to the manufacturer's guidelines and fire safety regulations."),

        ["Are protective equipment (ear defenders, goggles, gloves) provided?"] = ("Section 13", "Medium",
    "All employees must be provided with personal protective equipment (PPE) including ear defenders, safety goggles, and gloves. " +
    "Proper training on PPE use and maintenance should also be conducted."),

        ["Is adequate ventilation and lighting maintained in work areas?"] = ("Section 16", "Medium",
    "Work areas must be properly ventilated to remove airborne contaminants and maintain a healthy working environment. " +
    "Adequate lighting must be provided to ensure clear visibility and prevent workplace accidents."),

        ["Are wood dust and waste properly disposed of to prevent fire hazards?"] = ("Section 14", "High",
    "All wood dust, chips, and waste materials must be collected and removed from the premises regularly. " +
    "Disposal must be carried out using safe methods to prevent the buildup of combustible materials."),

        ["Are first-aid boxes available?"] = ("Section 19", "High",
    "A fully stocked first-aid box must be available on site, containing supplies appropriate for the types of injuries likely to occur in a wood processing environment. " +
    "Contents should be checked and replenished regularly."),

        ["Are lifting equipment and tackle tested, marked, and logged?"] = ("Section 33", "Medium",
    "All lifting equipment and tackle must undergo periodic testing and inspection. " +
    "Equipment must be clearly marked with safe working loads and documented in a lifting tackle register."),

        ["Are safe walkways, floors, and stairs maintained?"] = ("Section 12", "Medium",
    "Walkways, floors, and stairs must be kept in good repair, free from obstructions and hazards such as loose boards or spills. " +
    "Anti-slip surfaces should be installed where necessary."),

        ["Are dangerous areas securely fenced or clearly marked?"] = ("Section 32", "High",
    "Areas containing dangerous machinery or processes must be securely fenced off or clearly marked with warning signs " +
    "to prevent unauthorized access and protect non-operational personnel."),

        ["Is there a register for accidents and dangerous occurrences?"] = ("Section 50", "Medium",
    "A formal register must be maintained, recording all accidents and dangerous occurrences. " +
    "These records should be reviewed periodically to identify safety trends and implement corrective actions."),


        // Warehouses
        ["Is the building safe and structurally sound?"] = ("Section 5", "High",
    "The warehouse building must be inspected to ensure its structural integrity and safety. " +
    "Any structural weaknesses must be addressed immediately to prevent accidents or collapse."),

        ["Are fire extinguishers strategically placed and maintained?"] = ("Section 45", "High",
    "Fire extinguishers must be installed in easily accessible locations across the warehouse. " +
    "They must be serviced regularly to ensure they are functional in case of a fire emergency."),

        ["Are racks and shelves secure and not overloaded?"] = ("Section 12", "Medium",
    "All storage racks and shelves must be securely anchored and arranged to prevent tipping. " +
    "Load limits should be observed and clearly marked to avoid overloading."),

        ["Are gangways clear of obstruction?"] = ("Section 12", "Medium",
    "All gangways and passageways must be kept clear of goods, waste, or obstructions " +
    "to allow safe movement of personnel and quick evacuation in emergencies."),

        ["Are safe access and escape routes maintained?"] = ("Section 45", "High",
    "Clear and unobstructed access and escape routes must be maintained at all times. " +
    "Exit doors should be clearly marked and easily opened without obstruction."),

        ["Is mechanical lifting equipment tested and safe?"] = ("Section 33", "Medium",
    "All forklifts, hoists, and lifting equipment used within the warehouse must undergo periodic safety testing and maintenance checks. " +
    "Any faulty equipment must be taken out of service until repaired."),

        ["Are warning notices displayed for hazardous areas?"] = ("Section 32", "High",
    "Warning signs must be prominently displayed around hazardous zones such as chemical storage areas, forklift paths, or restricted sections. " +
    "These signs must comply with legal visibility standards."),

        ["Is suitable lighting provided in all areas?"] = ("Section 15", "Low",
    "Adequate lighting must be provided in all working areas, including storage sections and emergency exits. " +
    "Lighting should be maintained to avoid dark spots that may lead to accidents."),

        ["Is there a first-aid box available?"] = ("Section 19", "High",
    "A first-aid box stocked with appropriate medical supplies must be available and easily accessible within the warehouse. " +
    "It should be inspected regularly and restocked as required."),

        ["Is drinking water and sanitary accommodation adequate?"] = ("Section 18", "Medium",
    "Workers must be provided with safe drinking water and access to clean sanitary facilities. " +
    "These amenities must be maintained in good condition and comply with hygiene regulations."),

        ["Are protective clothing and lifting aids provided where needed?"] = ("Section 13", "Medium",
    "Where required, employees must be provided with protective clothing such as gloves, helmets, and safety shoes. " +
    "Lifting aids must be supplied to reduce manual handling risks."),

        ["Is fire-fighting training conducted for staff?"] = ("Section 45", "Medium",
    "Staff must undergo regular fire safety training, including the use of fire extinguishers and emergency evacuation procedures. " +
    "Training records should be maintained for compliance verification."),

        ["Is ventilation adequate to prevent buildup of harmful vapours?"] = ("Section 16", "Medium",
    "Adequate ventilation must be provided, especially in areas where chemicals or flammable goods are stored, " +
    "to prevent the buildup of harmful or explosive vapours."),

        // Sachet Water Production
        ["Is the premises clean and hygienic?"] = ("Section 14", "Medium",
    "The production premises must be kept clean and hygienic at all times to ensure food safety and prevent contamination. " +
    "Regular cleaning schedules should be implemented and documented."),

        ["Are machines used for production regularly cleaned and maintained?"] = ("Section 48", "Medium",
    "All production machines should undergo routine cleaning and maintenance to ensure safe operation and prevent contamination of sachet water."),

        ["Is potable water used and periodically tested?"] = ("Section 18", "High",
    "Only potable water that meets Ghana Standards Authority requirements should be used for sachet water production. " +
    "Water should be tested periodically for chemical and biological safety."),

        ["Are protective clothing, gloves, and head covers provided for workers?"] = ("Section 13", "Medium",
    "Workers must be provided with appropriate protective clothing such as gloves, head covers, and aprons " +
    "to minimize contamination risks during production."),

        ["Are sanitary facilities clean and segregated by sex?"] = ("Section 18", "Medium",
    "Clean and well-maintained sanitary facilities should be available on-site and clearly segregated by sex " +
    "to ensure hygiene and worker comfort."),

        ["Is food or drink consumption prohibited in production areas?"] = ("Section 17", "Low",
    "Clear signage must be displayed to prohibit food or drink consumption within production areas, " +
    "maintaining hygiene and preventing contamination."),

        ["Is adequate lighting and ventilation available in the plant?"] = ("Section 16", "Medium",
    "Proper lighting and ventilation must be provided throughout the production plant to ensure safe working conditions " +
    "and compliance with occupational health standards."),

        ["Is a first-aid box maintained?"] = ("Section 19", "High",
    "A fully stocked first-aid box must be maintained on-site, easily accessible to all workers in case of injury or medical emergencies."),

        ["Are emergency exits functional and accessible?"] = ("Section 45", "High",
    "Emergency exits must remain functional, clearly marked, and unobstructed at all times to ensure quick evacuation " +
    "in the event of an emergency."),

        ["Are accidents and injuries properly recorded and reported?"] = ("Section 50", "Medium",
    "All workplace accidents and injuries must be documented in a register and reported according to " +
    "the Factories, Offices and Shops Act, 1970 requirements."),

        ["Are cleaning and disinfection schedules maintained?"] = ("Section 14", "Medium",
    "Regular cleaning and disinfection schedules must be implemented and recorded to maintain hygiene throughout the facility."),

        ["Are proper drainage and waste disposal systems in place?"] = ("Section 14", "Medium",
    "Adequate drainage systems must be provided to prevent water stagnation, and waste disposal methods should comply " +
    "with environmental and health regulations."),

        ["Is registration certificate displayed?"] = ("Section 3", "High",
    "A valid registration certificate issued by the relevant authority must be displayed prominently within the premises " +
    "as proof of compliance with legal requirements."),

        ["Are packaging materials stored hygienically?"] = ("Section 14", "Medium",
    "All packaging materials must be stored in clean, dry, and hygienic conditions to avoid contamination " +
    "prior to use in sachet water production."),

        // Offices
        ["Is the premises registered?"] = ("Section 3", "High",
    "The office premises must be registered with the appropriate authority, " +
    "and a valid registration certificate should be displayed to confirm compliance with regulatory requirements."),

        ["Is the office clean, ventilated and well lit?"] = ("Section 14", "Medium",
    "The office environment must be kept clean, adequately ventilated, and properly lit " +
    "to promote a healthy and productive workspace for all employees."),

        ["Are fire extinguishers available and easily accessible?"] = ("Section 45", "High",
    "Appropriate fire extinguishers must be installed at visible and accessible locations within the office. " +
    "Regular servicing and inspection should be conducted to ensure they remain operational."),

        ["Is drinking water provided and labeled?"] = ("Section 18", "Medium",
    "Potable drinking water must be provided for all staff members, clearly labeled to avoid misuse or confusion. " +
    "Water sources should be regularly inspected for quality assurance."),

        ["Are suitable washing and sanitary conveniences available?"] = ("Section 18", "Medium",
    "Sanitary facilities such as toilets and washbasins must be provided in sufficient numbers " +
    "and maintained in a clean and functional condition at all times."),

        ["Are first-aid facilities provided?"] = ("Section 19", "High",
    "A well-stocked first-aid box must be available on the office premises. " +
    "The contents should be regularly checked, and trained first-aid personnel should be designated."),

        ["Are escape routes and emergency exits available and unobstructed?"] = ("Section 45", "High",
    "All emergency exits and escape routes must be clearly marked, easily accessible, " +
    "and free from obstruction to ensure safe evacuation in the event of an emergency."),

        ["Is adequate working space (minimum 40 sq ft per person) provided?"] = ("Section 12", "Medium",
    "A minimum working space of 40 square feet per person must be maintained in office layouts " +
    "to ensure comfort, safety, and compliance with legal standards."),

        ["Are floors, stairways, and corridors in good repair and free from obstruction?"] = ("Section 12", "Medium",
    "Floors, stairways, and corridors must be kept in sound condition, free from hazards and obstructions " +
    "that could cause slips, trips, or falls."),

        ["Are any records of accidents or dangerous occurrences maintained?"] = ("Section 50", "Medium",
    "Accurate and up-to-date records of any workplace accidents or dangerous occurrences must be maintained " +
    "as required by the Factories, Offices, and Shops Act, 1970."),

        ["Are dust and fumes properly controlled (if applicable)?"] = ("Section 17", "Medium",
    "If dust or fumes are present due to office operations, effective control measures such as extraction systems " +
    "or ventilation must be implemented to protect staff health."),

        // Shops
        ["Is the shop registered and certificate displayed?"] = ("Section 3", "High",
    "The shop must be duly registered with the relevant authority, and a valid registration certificate " +
    "must be prominently displayed on the premises as proof of compliance."),

        ["Are fire safety arrangements in place (extinguishers, escape routes)?"] = ("Section 45", "High",
    "The shop must have adequate fire safety arrangements, including strategically placed fire extinguishers " +
    "and clearly marked, unobstructed escape routes for emergency situations."),

        ["Is lighting and ventilation adequate for staff and customers?"] = ("Section 16", "Medium",
    "Proper lighting and ventilation must be provided throughout the shop to ensure a comfortable and safe " +
    "environment for both staff and customers."),

        ["Are sanitary conveniences clean and accessible?"] = ("Section 18", "Medium",
    "Clean and accessible sanitary facilities must be available on the premises for staff and, where applicable, " +
    "for customers. These facilities should be regularly maintained."),

        ["Is potable drinking water available?"] = ("Section 18", "Medium",
    "Potable drinking water must be provided for staff, with appropriate labeling and regular quality checks " +
    "to ensure it is safe for consumption."),

        ["Is a first-aid box present and in good condition?"] = ("Section 19", "High",
    "A fully stocked first-aid box must be available on the premises, easily accessible, and maintained in good condition. " +
    "Regular checks should be conducted to replace expired items."),

        ["Are accident records maintained?"] = ("Section 50", "Medium",
    "Accident records must be maintained in accordance with regulatory requirements, documenting any incidents " +
    "that occur on the premises for reference and compliance."),

        ["Are walkways and exits unobstructed?"] = ("Section 12", "Medium",
    "All walkways and exits must remain clear of obstruction at all times to facilitate safe movement within the premises " +
    "and ensure efficient evacuation during emergencies."),

        ["Is suitable seating provided for workers (if applicable)?"] = ("Section 13", "Low",
    "Where applicable, suitable seating must be provided for workers, especially those engaged in duties that require " +
    "prolonged standing, to promote staff welfare and comfort."),

        ["Are floors sound, clean, and free from hazards?"] = ("Section 12", "Medium",
    "Shop floors must be kept in good repair, clean, and free from hazards such as slippery surfaces or uneven flooring " +
    "that could pose a risk to staff or customers."),

        ["Is protective equipment provided where necessary?"] = ("Section 13", "Medium",
    "Where shop operations expose staff to potential hazards, appropriate protective equipment such as gloves or aprons " +
    "must be provided and enforced."),

        // Manufacturing Companies
        ["Is the premises registered with an updated certificate?"] = ("Section 3", "High",
    "The manufacturing premises must be registered with the relevant authorities, ensuring that the registration certificate " +
    "is valid, updated, and prominently displayed on-site."),

        ["Are all machinery and moving parts safely guarded?"] = ("Section 31", "High",
    "All machinery and moving parts must have appropriate safety guards installed to prevent accidental contact, " +
    "ensuring worker safety in compliance with statutory requirements."),

        ["Is lifting equipment tested, certified and safe?"] = ("Section 33", "High",
    "All lifting equipment used on the premises must be regularly tested and certified by competent professionals " +
    "to guarantee operational safety and compliance with legal standards."),

        ["Are fire safety and escape provisions adequate?"] = ("Section 45", "High",
    "Fire safety measures, including extinguishers, alarms, and clearly marked escape routes, must be implemented " +
    "and maintained to safeguard workers and property during emergencies."),

        ["Are floors, stairs, and passages in sound condition and unobstructed?"] = ("Section 12", "Medium",
    "All floors, stairs, and passages must be kept in good condition, free from obstructions, damage, or hazards " +
    "that could cause slips, trips, or falls."),

        ["Are proper records of accidents, diseases, and dangerous occurrences kept?"] = ("Section 50", "Medium",
    "Accurate records of all accidents, occupational diseases, and dangerous occurrences must be maintained as part " +
    "of statutory compliance and for continuous safety monitoring."),

        ["Are suitable sanitary, washing, and drinking water facilities available?"] = ("Section 18", "Medium",
    "The premises must provide suitable sanitary conveniences, washing facilities, and potable drinking water for all workers, " +
    "maintained in a clean and functional state."),

        ["Is dust, fumes, and noise effectively controlled?"] = ("Section 17", "High",
    "Effective control measures must be in place to minimize exposure to harmful dust, fumes, and noise levels, " +
    "protecting the health and safety of all employees."),

        ["Are protective clothing and safety equipment provided?"] = ("Section 13", "Medium",
    "Appropriate protective clothing and safety equipment must be provided to all workers exposed to workplace hazards, " +
    "and their usage must be enforced."),

        ["Are emergency stops on machines functional?"] = ("Section 31", "High",
    "All machinery must be equipped with functional emergency stop devices that are easily accessible and regularly " +
    "tested to ensure immediate shutdown in case of danger."),

        ["Are workrooms adequately ventilated and lit?"] = ("Section 16", "Medium",
    "Workrooms must be equipped with adequate ventilation and lighting systems to create a safe, comfortable, " +
    "and compliant working environment."),

        ["Are emergency drills conducted?"] = ("Section 45", "Medium",
    "Regular emergency evacuation and fire drills must be conducted to train staff in emergency response procedures, " +
    "ensuring preparedness for real-life situations."),

        ["Is medical supervision provided for hazardous processes?"] = ("Section 29", "High",
    "For processes involving hazardous substances or activities, medical supervision must be arranged to monitor worker health " +
    "and provide necessary interventions."),

        ["Are workers trained in safe operation of equipment?"] = ("Section 13", "Medium",
    "All workers must receive adequate training in the safe operation of machinery and equipment, including understanding " +
    "safety protocols and emergency procedures.")
    };
}
