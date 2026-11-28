(() => {
    const root=document.documentElement;
    const saved=key=>{try{return localStorage.getItem(key);}catch{return null;}};
    const persist=(key,value)=>{try{localStorage.setItem(key,value);}catch{}};
    let language=saved('garowe.language')==='so'?'so':'en';
    let theme=saved('garowe.theme');
    if(theme!=='dark'&&theme!=='light')theme=matchMedia('(prefers-color-scheme: dark)').matches?'dark':'light';
    root.dataset.theme=theme;root.lang=language;
    const words={
        'Language':'Luqadda','Display preferences':'Dejimaha muuqaalka','Toggle dark mode':'Beddel muuqaalka mugdiga',
        'Workspace':'Goobta shaqada','Overview':'Dulmar','My workspace':'Goobtayda shaqada','My agenda':'Ajandahayga',
        'HR':'Shaqaalaha','Employees':'Shaqaalaha','Employee':'Shaqaale','Employee directory':'Liiska shaqaalaha',
        'Register employee':'Diiwaangeli shaqaale','Employee reports':'Warbixinnada shaqaalaha','Organization structure':'Qaab-dhismeedka hay’adda',
        'Attendance':'Xaadirinta','Attendance & imports':'Xaadirinta iyo soo dejinta','Attendance reports':'Warbixinnada xaadirinta',
        'Corrections':'Sixitaannada','Attendance devices':'Qalabka xaadirinta','Leave':'Fasax','Leave requests':'Codsiyada fasaxa',
        'Leave requests & approvals':'Codsiyada iyo ansixinta fasaxa','Leave balances':'Haraaga fasaxa','Leave reports':'Warbixinnada fasaxa',
        'Salary':'Mushahar','Salary registration':'Diiwaangelinta mushaharka','Salary reports':'Warbixinnada mushaharka',
        'Payroll':'Mushahar-bixinta','Manage payroll':'Maamul mushahar-bixinta','Payroll reports':'Warbixinnada mushahar-bixinta',
        'Tasks':'Hawlaha','My tasks':'Hawlahayga','Task management':'Maamulka hawlaha','Manage tasks':'Maamul hawlaha',
        'Task reports':'Warbixinnada hawlaha','Task progress reports':'Warbixinnada horumarka hawlaha',
        'Meetings':'Shirarka','My meetings':'Shirarkayga','Manage meetings':'Maamul shirarka','Meeting reports':'Warbixinnada shirarka',
        'Saved recordings':'Duubisyada kaydsan','Assets':'Hantida','Asset register':'Diiwaanka hantida','Register asset':'Diiwaangeli hanti',
        'Asset reports':'Warbixinnada hantida','Fleet':'Gaadiidka','Fleet trips':'Safarrada gaadiidka','Vehicle register':'Diiwaanka gaadiidka',
        'Fleet reports & expenses':'Warbixinnada iyo kharashyada gaadiidka','Main reports':'Warbixinnada guud','Main settings':'Dejimaha guud',
        'Settings':'Dejimaha','Reports':'Warbixinnada','All reports':'Dhammaan warbixinnada','HR & payroll':'Shaqaalaha iyo mushahar-bixinta',
        'Tasks & meetings':'Hawlaha iyo shirarka','Assets & fleet':'Hantida iyo gaadiidka','Audit':'Raadraaca','Audit log':'Diiwaanka raadraaca',
        'Custom reports':'Warbixinno gaar ah','Design reports':'Habee warbixinnada','Open report →':'Fur warbixinta →',
        'Open register →':'Fur diiwaanka →','Reports & exports':'Warbixinno iyo dhoofin','available reports':'warbixinno la heli karo',
        'YOUR WORKSPACE':'GOOBTAADA SHAQADA','CITY ADMINISTRATION':'MAAMULKA MAGAALADA','REPORTING WORKSPACE':'GOOBTA WARBIXINNADA',
        'CITY REPORTING WORKSPACE':'WARBIXINNADA MAGAALADA','Workspace overview':'Dulmarka goobta shaqada',
        'A clear view of your people, work and resources.':'Aragti cad oo ku saabsan shaqaalaha, hawlaha iyo khayraadka.',
        'Welcome to your city workspace':'Ku soo dhawoow goobta shaqada ee magaalada',
        "Review today's attendance, follow up on requests and keep your teams moving.":'Dib u eeg xaadirinta maanta, la soco codsiyada oo hag kooxahaaga.',
        "Today's attendance →":'Xaadirinta maanta →','Open inbox':'Fur farriimaha','Active employees':'Shaqaalaha firfircoon',
        'Across your authorized branches':'Laamaha aad fasaxa u haysato','Leave awaiting action':'Fasaxyada sugaya tallaabo',
        'Open requests for review':'Codsiyada sugaya dib-u-eegista','Attendance today':'Xaadirinta maanta','Active branches':'Laamaha firfircoon',
        'Manage connected workplaces':'Maamul goobaha shaqada','People across branches':'Shaqaalaha laamaha','Current employee assignments':'Qoondaynta shaqaalaha',
        'View employees →':'Eeg shaqaalaha →',"Today's attendance":'Xaadirinta maanta','Attendance and approved leave coverage':'Xaadirinta iyo fasaxyada la ansixiyey',
        'coverage today':'xaadirinta maanta','Review attendance →':'Dib u eeg xaadirinta →','Latest payroll':'Mushahar-bixintii ugu dambaysay',
        'Net payable by currency':'Mushaharka saafiga ah lacag kasta','FINANCE':'MAALIYADDA','View payroll →':'Eeg mushahar-bixinta →',
        'Explore your modules':'Fur qaybaha nidaamka','Open a workspace to manage records, track progress or review reports.':'Fur qayb si aad u maamusho diiwaannada, horumarka ama warbixinnada.',
        'Profiles, employment and documents':'Xogta shaqaalaha, shaqaalaysiinta iyo dukumentiyada','Tasks & teams':'Hawlaha iyo kooxaha',
        'Assignments, responsibilities and progress':'Qoondaynta, masuuliyadaha iyo horumarka','Schedules, agendas and decisions':'Jadwallada, ajandayaasha iyo go’aannada',
        'Inventory, custody and condition':'Diiwaanka, haynta iyo xaaladda hantida','Vehicles, trips and maintenance':'Gaadiidka, safarrada iyo dayactirka',
        'Filter, review and export your data':'Shaandhee, eeg oo dhoofi xogtaada','No calculated payroll yet':'Weli ma jiro mushahar la xisaabiyey',
        'No employee assignments yet':'Weli ma jiraan shaqaale la qoondeeyey','Find a menu…':'Raadi qayb…','Find a menu':'Raadi qayb',
        'Expand all':'Fur dhammaan','Collapse all':'Xir dhammaan','No matching menus':'Qayb u dhiganta lama helin',
        'Inbox':'Farriimaha','Sign out':'Ka bax','Sign in':'Soo gal','Sign in →':'Soo gal →','Signing in…':'Waa lagu gelinayaa…',
        'WELCOME BACK':'SOO DHAWOOW','Sign in to your workspace':'Soo gal goobtaada shaqada',
        'Enter your username, email address or employee ID to continue.':'Geli magaca isticmaalaha, iimaylka ama lambarka shaqaalaha.',
        'Username, email or employee ID':'Magaca isticmaalaha, iimaylka ama lambarka shaqaalaha',
        'Your username, email or employee ID':'Magacaaga, iimaylkaaga ama lambarka shaqaalaha','Password':'Furaha sirta ah',
        'Enter your password':'Geli furaha sirta ah','Forgot your password?':'Ma illowday furaha sirta ah?',
        'Forgot password?':'Ma illowday furaha sirta ah?','Show':'Muuji','Hide':'Qari','Show password':'Muuji furaha sirta ah',
        'Hide password':'Qari furaha sirta ah','Caps Lock is on.':'Caps Lock wuu shidan yahay.',
        'Use the account supplied by your administrator.':'Isticmaal koontada uu maamuluhu ku siiyey.',
        'Contact your administrator if you need access.':'La xiriir maamulaha haddii aad u baahan tahay koonto.',
        'OFFICIAL MUNICIPAL WORKSPACE':'GOOBTA RASMIGA AH EE DEGMADA','STAFF & ADMINISTRATION PORTAL':'BOGGA SHAQAALAHA IYO MAAMULKA',
        'Manage employees, coordinate teams and oversee city resources from one connected workspace.':'Maamul shaqaalaha, isku dubbarid kooxaha oo la soco khayraadka magaalada hal goob.',
        'People & attendance':'Shaqaalaha iyo xaadirinta','Meetings & decisions':'Shirarka iyo go’aannada','Payroll & leave':'Mushahar-bixinta iyo fasaxa',
        'Assets & equipment':'Hantida iyo qalabka','Fleet & transport':'Gaadiidka iyo safarrada','People · Operations · Resources':'Shaqaalaha · Hawlaha · Khayraadka',
        'Search':'Raadi','Clear':'Nadiifi','Save':'Kaydi','Cancel':'Ka noqo','Edit':'Wax ka beddel','Delete':'Tirtir','Details':'Faahfaahin',
        'View':'Eeg','Download':'Soo dejiso','Upload':'Soo geli','Back':'Dib u noqo','Next':'Xiga','Previous':'Hore','First':'Koowaad','Last':'Ugu dambeeya',
        'Apply filters':'Adeegso shaandhaynta','Clear filters':'Nadiifi shaandhaynta','Filters':'Shaandhaynta',
        'Download CSV':'Soo dejiso CSV','Download Excel':'Soo dejiso Excel','Download PDF':'Soo dejiso PDF','Download sample CSV':'Soo dejiso tusaalaha CSV',
        'Export data':'Dhoofi xogta','Upload CSV':'Soo geli CSV','CSV file':'Faylka CSV','Sample file, download and upload':'Fayl tusaale ah, soo dejin iyo soo gelin',
        'Records per page':'Diiwaannada boggiiba','All':'Dhammaan','All employees':'Dhammaan shaqaalaha','All branches':'Dhammaan laamaha',
        'All departments':'Dhammaan waaxaha','All sub-departments':'Dhammaan waax-hoosaadyada','All statuses':'Dhammaan xaaladaha',
        'From':'Laga bilaabo','To':'Ilaa','Employee ID':'Lambarka shaqaalaha','Employee number':'Lambarka shaqaalaha','Full name':'Magaca oo buuxa',
        'Name':'Magaca','Email':'Iimayl','Email address':'Cinwaanka iimaylka','Username':'Magaca isticmaalaha','Phone':'Telefoonka',
        'Phone number':'Lambarka telefoonka','Branch':'Laanta','Department':'Waaxda','Sub-department':'Waax-hoosaadka','Position':'Jagada',
        'Job title':'Jagada shaqada','Status':'Xaaladda','Actions':'Tallaabooyinka','Date':'Taariikhda','Time':'Waqtiga',
        'Check-in':'Soo-gelid','Check-out':'Bixid','Time in':'Waqtiga soo-gelidda','Time out':'Waqtiga bixidda','Hours':'Saacadaha',
        'Worked hours':'Saacadaha la shaqeeyey','Late minutes':'Daqiiqadaha soo-daahidda','Absent':'Maqan','Present':'Jooga','Late':'Soo daahay',
        'Active':'Firfircoon','Inactive':'Aan firfircoonayn','Pending':'Sugaya','Approved':'La ansixiyey','Rejected':'La diiday',
        'Draft':'Qabyo','Paid':'La bixiyey','Locked':'La xiray','Open':'Furan','In progress':'Socda','Completed':'La dhammaystiray','Cancelled':'La joojiyey',
        'Basic salary':'Mushaharka aasaasiga ah','Basic pay':'Mushaharka aasaasiga ah','Gross pay':'Wadarta mushaharka','Net pay':'Mushaharka saafiga ah',
        'Deductions':'Jaritaannada','Attendance deductions':'Jaritaannada xaadirinta','Amount':'Qaddarka','Currency':'Lacagta','Total':'Wadarta',
        'Payroll period':'Muddada mushahar-bixinta','Payslip':'Warqadda mushaharka','Generate payroll':'Diyaari mushahar-bixinta','Calculate':'Xisaabi',
        'Submit':'Gudbi','Approve':'Ansixi','Reject':'Diid','Mark paid':'Calaamadee in la bixiyey','Publish':'Daabac',
        'Title':'Cinwaanka','Description':'Sharaxaadda','Priority':'Mudnaanta','Due date':'Taariikhda kama dambaysta ah','Progress':'Horumarka',
        'Assigned to':'Loo qoondeeyey','Assignee':'Shaqaalaha loo qoondeeyey','Individual':'Qof keli ah','Team':'Koox','Subtasks':'Hawl-hoosaadyada',
        'Agenda':'Ajandaha','Minutes':'Qoraalka shirka','Documents':'Dukumentiyada','Location':'Goobta','Start':'Bilow','End':'Dhammaad',
        'Join meeting':'Ku biir shirka','End meeting':'Dhammee shirka','Copy link':'Nuqul xiriiriyaha','Participants':'Ka qaybgalayaasha',
        'Users & permissions':'Isticmaalayaasha iyo oggolaanshaha','Manage roles':'Maamul doorarka','+ Add account':'+ Ku dar koonto',
        'Account directory':'Liiska koontooyinka','Create an account':'Abuur koonto','Find a user':'Raadi isticmaale','Matching accounts':'Koontooyinka la helay',
        'Access guide →':'Hagaha oggolaanshaha →','Roles & responsibilities':'Doorarka iyo masuuliyadaha','Menu access':'Oggolaanshaha qaybaha',
        'Branches':'Laamaha','Security':'Amniga','Employee link':'Xiriirinta shaqaalaha','Manage':'Maamul','Roles':'Doorarka',
        'Inherit':'Dhaxal','Allow':'Oggolow','Deny':'Diid','Security & sign-in':'Amniga iyo gelitaanka','Reset password':'Dib u deji furaha sirta ah',
        'Change password':'Beddel furaha sirta ah','Current password':'Furaha hadda','New password':'Furaha cusub','Confirm password':'Xaqiiji furaha',
        'Save password':'Kaydi furaha','Temporary password':'Fure ku meel gaar ah','General Secretary':'Xoghayaha Guud',
        'Management':'Maamulka','Finance':'Maaliyadda','Admin':'Maamule','Administrator':'Maamulaha','Human resources':'Shaqaalaha',
        'Employee workspace':'Goobta shaqaalaha','Nationality':'Jinsiyadda','Gender':'Jinsiga','Date of birth':'Taariikhda dhalashada',
        'Marital status':'Xaaladda guurka','Address':'Cinwaanka','Hire date':'Taariikhda shaqaalaysiinta','Emergency contact':'Xiriirka degdegga ah',
        'No users found':'Isticmaale lama helin','No reports available':'Warbixin lama heli karo','No records found':'Diiwaan lama helin',
        '↔ Scroll sideways to view all columns':'↔ Dhinac u rog si aad u aragto dhammaan tiirarka',
        'Toggle navigation':'Fur ama xir qaybaha','Close navigation':'Xir qaybaha','Skip to main content':'U gudub nuxurka muhiimka ah',
        'Main navigation':'Qaybaha nidaamka','Reports and exports':'Warbixinno iyo dhoofin','Report categories':'Qaybaha warbixinnada',
        'Net payable · latest calculated run':'Mushaharka saafiga ah · xisaabtii ugu dambaysay',
        'EAST AFRICA TIME · UTC+3':'WAQTIGA BARIGA AFRIKA · UTC+3',
        'Every report available to your account.':'Dhammaan warbixinnada koontadaadu heli karto.',
        'Employee register, hires and leavers':'Diiwaanka shaqaalaha, shaqaalaysiinta iyo bixitaanka',
        'Daily summaries, hours and lateness':'Soo koobidda maalinlaha, saacadaha iyo soo-daahidda',
        'Leave register and balances':'Diiwaanka iyo haraaga fasaxa','Pay runs, deductions and payment exports':'Mushahar-bixinta, jaritaannada iyo dhoofinta lacag-bixinta',
        'Salary versions and allowances':'Taariikhda mushaharka iyo gunnooyinka','Progress and completion by assignment':'Horumarka iyo dhammaystirka hawlaha',
        'Meeting activity and participation':'Hawlaha iyo ka qaybgalka shirarka','Inventory, custody, status and value':'Diiwaanka, haynta, xaaladda iyo qiimaha hantida',
        'Trips, mileage, expenses and service':'Safarrada, masaafada, kharashyada iyo dayactirka','Account, configuration and HR changes':'Isbeddellada koontooyinka, dejimaha iyo shaqaalaha',
        'Employee data reset':'Nadiifinta xogta shaqaalaha','Records to remove':'Diiwaannada la tirtirayo','Confirmation':'Xaqiijin'

    };
    const texts=new WeakMap(),attributes=new WeakMap(), originalTitle=document.title;
    const translate=source=>{
        if(words[source])return words[source];
        let match=source.match(/^(\d+) of (\d+) expected employees covered$/);
        if(match)return match[1]+' ka mid ah '+match[2]+' shaqaale ayaa xaadir ah';
        match=source.match(/^(\d+) (covered|remaining|employees|records)$/);
        if(match)return match[1]+' '+({covered:'xaadir ah',remaining:'hadhay',employees:'shaqaale',records:'diiwaan'})[match[2]];
        match=source.match(/^(Sunday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday), (\d+) (January|February|March|April|May|June|July|August|September|October|November|December) (\d{4})$/);
        if(match){const days={Sunday:'Axad',Monday:'Isniin',Tuesday:'Talaado',Wednesday:'Arbaco',Thursday:'Khamiis',Friday:'Jimce',Saturday:'Sabti'};const months={January:'Jannaayo',February:'Febraayo',March:'Maarso',April:'Abriil',May:'Maajo',June:'Juun',July:'Luulyo',August:'Agoosto',September:'Sebtembar',October:'Oktoobar',November:'Nofeembar',December:'Diseembar'};return days[match[1]]+', '+match[2]+' '+months[match[3]]+' '+match[4];}
        match=source.match(/^(.+) filters$/);if(match&&words[match[1]])return 'Shaandhaynta '+words[match[1]];
        return source;
    };
    function text(node){
        const parent=node.parentElement;
        if(!parent||parent.closest('script,style,textarea,code,pre,[contenteditable],[data-no-translate],.user-card h2'))return;
        if(parent.closest('tbody td')&&!parent.closest('button,a,label,.badge,summary'))return;
        const value=node.nodeValue;let record=texts.get(node);
        if(!record||value!==record.last&&value!==record.source)record={source:value,last:value};
        const source=record.source,trimmed=source.trim();
        if(!trimmed)return;
        if(parent.tagName==='OPTION'&&!parent.hasAttribute('value'))parent.value=parent.textContent;
        const target=language==='so'?translate(trimmed):trimmed;
        const next=source.slice(0,source.indexOf(trimmed))+target+source.slice(source.indexOf(trimmed)+trimmed.length);
        record.last=next;texts.set(node,record);if(value!==next)node.nodeValue=next;
    }
    function apply(){
        const walker=document.createTreeWalker(document.body,NodeFilter.SHOW_TEXT);let node;
        while((node=walker.nextNode()))text(node);
        document.querySelectorAll('[placeholder],[aria-label],[title]').forEach(element=>{
            if(element.closest('[data-no-translate],script,style'))return;
            let records=attributes.get(element)||{};
            for(const attr of ['placeholder','aria-label','title']){
                if(!element.hasAttribute(attr))continue;
                const value=element.getAttribute(attr);let record=records[attr];
                if(!record||value!==record.last&&value!==record.source)record={source:value,last:value};
                const next=language==='so'?translate(record.source):record.source;
                record.last=next;records[attr]=record;if(value!==next)element.setAttribute(attr,next);
            }
            attributes.set(element,records);
        });
        root.lang=language;
        const parts=originalTitle.split(' · ');parts[0]=language==='so'?translate(parts[0]):parts[0];document.title=parts.join(' · ');
    }
    function ready(){
        const select=document.getElementById('workspace-language'),button=document.getElementById('workspace-theme');
        const themeLabel=()=>{if(button){button.setAttribute('aria-pressed',String(theme==='dark'));button.setAttribute('aria-label',language==='so'?(theme==='dark'?'U beddel muuqaalka iftiinka':'U beddel muuqaalka mugdiga'):(theme==='dark'?'Switch to light mode':'Switch to dark mode'));}};
        if(select){select.value=language;select.addEventListener('change',()=>{language=select.value==='so'?'so':'en';persist('garowe.language',language);apply();themeLabel();});}
        if(button)button.addEventListener('click',()=>{theme=theme==='dark'?'light':'dark';root.dataset.theme=theme;persist('garowe.theme',theme);themeLabel();});
        apply();themeLabel();
        let pending=false;
        new MutationObserver(()=>{if(pending)return;pending=true;requestAnimationFrame(()=>{pending=false;apply();});}).observe(document.body,{childList:true,subtree:true});
    }
    if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',ready);else ready();
})();
