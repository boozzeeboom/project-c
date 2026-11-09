// Elio engine v1.4 — регламент ELIO_RULES_FINAL.md v1.4
// Основные нововведения: нейтральный/заявленный нуль, поддержка 1→1, оттеснение,
// счёт «Ц» по ФРАКЦИИ + следующие собственные ходы держателя (holdTarget), три режима хода,
// два типа игры (Основной D1 = «убил=победил», Длинный D2 = выбывание), лимит 90.
// v1.3: инертные фигуры не удерживают «Ц»; pass-режим с passRule random/repeat/back.
(function(){
  const FACTIONS = ['plus','minus','zero'];
  const FACTION_NAME = {plus:'Плюсы',minus:'Минусы',zero:'Нули'};
  const FACTION_COLOR = {plus:'#5b9bd5', minus:'#e05a5a', zero:'#9aa0a6'};

  const ZERO_TYPES = ['parliament','free','courier','engineer','saboteur','skiff'];
  const TYPE_NAME = {
    line:'Линейный', battleship:'Линкор', bombardier:'Бомбардир', heavy:'Тяжёлый крейсер', frigate:'Фрегат', corvette:'Корвет', destroyer:'Миноносец',
    tender:'Тендер', skiff:'Скиталец',
    parliament:'Капитан-Линейный', free:'Свободный', courier:'Курьер', engineer:'Инженер', saboteur:'Диверсант'
  };
  const PIECE_VALUE = {line:7,battleship:6,bombardier:4,heavy:5,frigate:3,corvette:2,destroyer:1,tender:1,skiff:1,parliament:4,free:2,courier:1.5,engineer:2.5,saboteur:2};

  // v1.3: только Lineyniy и TК — тяжёлые; корвет больше не «тяжёлый».
  function isHeavy(t){ return t==='line'||t==='battleship'||t==='heavy'; }
  function isNullType(t){ return ZERO_TYPES.indexOf(t)!==-1; }
  function isPawnType(t){ return t==='tender'||t==='skiff'; }
  function hasPawn(s,f){ return s.pieces.some(p=>p.alive&&!p.inert&&p.faction===f&&isPawnType(p.type)); }

  const FIELD_CENTERS = {plus:[0,-3], minus:[-3,3], zero:[3,0]};
  const FIELD_INFO = [
    {id:'plus',  name:'ПЛЮСЫ',  color:'#5b9bd5', center:FIELD_CENTERS.plus,  prefix:'P', yellow:[0,-1]},
    {id:'minus', name:'МИНУСЫ', color:'#e05a5a', center:FIELD_CENTERS.minus, prefix:'M', yellow:[-1,1]},
    {id:'zero',  name:'НУЛИ',   color:'#9aa0a6', center:FIELD_CENTERS.zero,  prefix:'Z', yellow:[1,0]},
  ];
  const DISPUTED = [
    {q:1,r:-1,code:'PZ-1'},{q:2,r:-2,code:'PZ-2'},
    {q:0,r:1,code:'MZ-1'},{q:0,r:2,code:'MZ-2'},
    {q:-1,r:0,code:'PM-1'},{q:-2,r:0,code:'PM-2'},
  ];
  const INNER_GATES = ['0,-1','-1,1','1,0'];   // жёлтые ворота
  const GATE_SET = new Set(INNER_GATES);
  const DISPUTED_SET = new Set(DISPUTED.map(d=>d.q+','+d.r));
  const NB = [[1,0],[0,1],[-1,1],[-1,0],[0,-1],[1,-1]];
  const LIMIT = 90;
  const LIMIT15 = 120;
  const PAWN_LOCAL = ['A11','B6','C1','B2','A3'];

  function dist(a,b){const dq=b[0]-a[0],dr=b[1]-a[1];return (Math.abs(dq)+Math.abs(dr)+Math.abs(dq+dr))/2;}
  function patch(cx,cy){const o=[];for(let q=-2;q<=2;q++)for(let r=-2;r<=2;r++)if(Math.abs(q)+Math.abs(r)+Math.abs(q+r)<=4)o.push([cx+q,cy+r]);return o;}

  function numberPetal(f){
    const pc=f.center;
    const cells=patch(pc[0],pc[1]);
    const r1=cells.filter(c=>dist(pc,c)===1);
    const r2=cells.filter(c=>dist(pc,c)===2);
    function ang(c){return Math.atan2(c[1]-pc[1], c[0]-pc[0]);}
    const ya=ang(f.yellow);
    function rot(a){let x=a-ya; while(x<0)x+=2*Math.PI; return x;}
    r2.sort((a,b)=>rot(ang(a))-rot(ang(b)));
    r1.sort((a,b)=>rot(ang(a))-rot(ang(b)));
    const mapping=new Map();
    r2.forEach((c,i)=>mapping.set(c[0]+','+c[1], f.prefix+'-A'+(i+1)));
    r1.forEach((c,i)=>mapping.set(c[0]+','+c[1], f.prefix+'-B'+(i+1)));
    mapping.set(pc[0]+','+pc[1], f.prefix+'-C1');
    return mapping;
  }
  const codeMap=new Map();
  FIELD_INFO.forEach(f=>{const m=numberPetal(f);for(const [k,v] of m)codeMap.set(k,v);});
  DISPUTED.forEach(d=>codeMap.set(d.q+','+d.r, d.code));
  codeMap.set('0,0','Ц');

  const KEY='q,r';
  function neighbors(q,r){return NB.map(d=>[q+d[0],r+d[1]]);}
  function isValidCell(state,q,r){return state.cells.has(q+','+r);}
  function cellAt(state,q,r){return state.cells.get(q+','+r)||null;}
  function pieceAt(state,q,r){const c=cellAt(state,q,r);return c&&c.piece?state.pieces.find(p=>p.id===c.piece)||null:null;}
  // Инертные фигуры выбывшей фракции остаются препятствиями, но не удерживают «Ц».
  function occupantAt(state){const p=pieceAt(state,0,0);return p&&!p.inert?p:null;}

  // Заявка: нуль "заявлен", если поддерживает кого-то или стоит на «Ц»/жёлтых воротах.
  function isOnGateOrCenter(piece){
    const k=piece.q+','+piece.r;
    return k==='0,0'||GATE_SET.has(k);
  }
  function isDeclaredZero(p){ return isNullType(p.type)&&(!!p.supportTarget||isOnGateOrCenter(p)); }
  function isNeutralZero(p){ return isNullType(p.type)&&!isDeclaredZero(p); }

  function pieceById(state,id){ return state.pieces.find(p=>p.id===id)||null; }

  // Кто может взять данного нуля (для UI и логики).
  // Возвращает: null (нельзя), 'both' (обе военные), или {mode:'opposite', supportedFaction}.
  function zeroVulnerability(state, targetZero){
    if(!isNullType(targetZero.type)) return null;
    if(isOnGateOrCenter(targetZero)) return 'both';
    if(!targetZero.supportTarget) return null;
    const target=pieceById(state,targetZero.supportTarget);
    if(!target||!target.alive) return null;
    if(isNullType(target.type)) return 'both';          // щит на нуль
    return {mode:'opposite', supportedFaction:target.faction};
  }
  function canCaptureZero(state, attackerFaction, targetZero){
    const v=zeroVulnerability(state,targetZero);
    if(!v) return false;
    if(v==='both') return true;
    if(v.mode==='opposite'){
      // «противоположная военная фракция» = военная, не равная поддерживаемому F
      if(attackerFaction===v.supportedFaction) return false;
      if(attackerFaction==='zero') return false;        // нули не бьют
      return true;
    }
    return false;
  }

  // Лестница защиты 0/1/2
  function protLevel(state,piece){
    let level=0;
    // 2: соседний нуль поддерживает ИМЕННО эту фигуру
    for(const n of neighbors(piece.q,piece.r)){
      const np=pieceAt(state,n[0],n[1]);
      if(np&&isNullType(np.type)&&np.supportTarget===piece.id){ level=2; break; }
    }
    if(level===2) return 2;
    // 1: соседняя своя Линейная или Инженер (аура)
    for(const n of neighbors(piece.q,piece.r)){
      const np=pieceAt(state,n[0],n[1]);
      if(np&&np.faction===piece.faction&&(np.type==='line'||np.type==='engineer')){ level=1; break; }
    }
    return level;
  }
  function canCapture(state,attacker,target){
    if(isNullType(attacker.type)) return false;         // нули не бьют
    if(isNullType(target.type)) return canCaptureZero(state,attacker.faction,target);
    const lv=protLevel(state,target);
    if(lv===2) return false;                            // поддержка абсолютна
    if(lv===1) return isHeavy(attacker.type);
    return true;
  }

  function straightCandidates(state,piece,maxStep){
    const cands=[];
    for(const d of NB){
      for(let s=1;s<=maxStep;s++){
        const tq=piece.q+d[0]*s, tr=piece.r+d[1]*s;
        if(!isValidCell(state,tq,tr)) { break; }
        if(s>1){
          const mid=pieceAt(state,piece.q+d[0]*(s-1),piece.r+d[1]*(s-1));
          if(mid){ break; }
        }
        const tp=pieceAt(state,tq,tr);
        if(tp&&tp.faction===piece.faction){ break; }
        cands.push({q:tq,r:tr});
        if(tp || (tq===0&&tr===0)){ break; }                                 // военная или заявленный нуль: остановились
      }
    }
    return cands;
  }

  function legalMoves(state,pieceId){
    const piece=pieceById(state,pieceId);
    if(!piece||!piece.alive||piece.inert) return [];
    if(state.gameOver) return [];
    if(piece.faction!==state.turnFaction) return [];
    if(state.firstMovePawn && !(state.firstMoveDone&&state.firstMoveDone[piece.faction]) && hasPawn(state,piece.faction) && !isPawnType(piece.type)) return [];
    const res=[], seen=new Set();
    const push=(q,r,extra)=>{
      if(!isValidCell(state,q,r)) return;          // не выпускаем фигуры за доску
      const key=q+','+(extra&&extra.displace?'D':'')+r;
      if(seen.has(key)) return;
      seen.add(key); res.push(Object.assign({q,r,capture:false},extra||{},{pieceId}));
    };
    const t=piece.type;
    if(t==='line'||t==='parliament'||t==='saboteur'||t==='tender'||t==='skiff'){
      for(const n of neighbors(piece.q,piece.r)) push(n[0],n[1]);
    } else if(t==='battleship'){ straightCandidates(state,piece,12).forEach(c=>push(c.q,c.r));
    } else if(t==='bombardier'){
      for(const d of NB){
        for(let step=2;step<=3;step++){
          const tq=piece.q+d[0]*step, tr=piece.r+d[1]*step;
          if(!isValidCell(state,tq,tr)) continue;
          if(tq===0&&tr===0) continue;
          let crossesCenter=false;
          for(let k=1;k<step;k++){
            if(piece.q+d[0]*k===0&&piece.r+d[1]*k===0){crossesCenter=true;break;}
          }
          if(crossesCenter) continue;
          push(tq,tr);
        }
      }
    } else if(t==='heavy'){ straightCandidates(state,piece,3).forEach(c=>push(c.q,c.r));
    } else if(t==='frigate'){ straightCandidates(state,piece,2).forEach(c=>push(c.q,c.r));
    } else if(t==='corvette'){
      for(let q=-2;q<=2;q++)for(let r=-2;r<=2;r++){
        if((Math.abs(q)+Math.abs(r)+Math.abs(q+r))/2!==2)continue;
        const tq=piece.q+q, tr=piece.r+r;
        if(!isValidCell(state,tq,tr))continue;
        // прыжок Корвета может пересекать «Ц»; приземление на «Ц» всё равно только с ворот (ниже)
        push(tq,tr);
      }
    } else if(t==='destroyer'){
      for(const n of neighbors(piece.q,piece.r)) push(n[0],n[1]);
      for(const d of NB){
        const tq=piece.q+d[0]*2, tr=piece.r+d[1]*2;
        if(!isValidCell(state,tq,tr))continue;
        if(pieceAt(state,tq,tr))continue;
        const midQ=piece.q+d[0], midR=piece.r+d[1];
        if(midQ===0&&midR===0)continue;
        const mid=pieceAt(state,midQ,midR);
        if(mid)continue;
        push(tq,tr,{dash:true});
      }
    } else if(t==='free'||t==='courier'||t==='engineer'){
      const max=t==='free'?3:(t==='courier'?4:2);
      straightCandidates(state,piece,max).forEach(c=>push(c.q,c.r));
    }

    const out=[];
    for(const c of res){
      const tp=pieceAt(state,c.q,c.r);
      if(tp&&tp.faction===piece.faction) continue;
      if(tp&&isNullType(piece.type)) continue;      // нули не бьют и не занимают занятые клетки
      if(tp){
        if(isNullType(tp.type)){
          // заявленный нуль — только если наш военный может взять по правилам §5
          if(!canCaptureZero(state,piece.faction,tp)) continue;
          out.push({pieceId,q:c.q,r:c.r,capture:true});
        } else {
          const lv=protLevel(state,tp);
          if(lv===2){continue;}
          if(lv===1&&!isHeavy(piece.type)) continue;    // корвет не пробивает щит
          out.push({pieceId,q:c.q,r:c.r,capture:true});
        }
      } else {
        out.push({pieceId,q:c.q,r:c.r,capture:false});
      }
      // вход на Ц — только с жёлтых ворот
      if(out.length&&out[out.length-1].q===0&&out[out.length-1].r===0){
        const k=piece.q+','+piece.r;
        if(!GATE_SET.has(k)) out.pop();
      }
    }
    // Оттеснение нейтральных нулей — только военной фигурой (§7)
    if(!isNullType(piece.type)){
      for(const n of neighbors(piece.q,piece.r)){
        const zp=pieceAt(state,n[0],n[1]);
        if(zp&&isNeutralZero(zp)){
          const dz=[zp.q-piece.q, zp.r-piece.r];
          const pz=[zp.q+dz[0], zp.r+dz[1]];
          if(isValidCell(state,pz[0],pz[1])&&!pieceAt(state,pz[0],pz[1])
             &&!(pz[0]===0&&pz[1]===0)&&!GATE_SET.has(pz[0]+','+pz[1])&&!DISPUTED_SET.has(pz[0]+','+pz[1])){
            out.push({pieceId,q:zp.q,r:zp.r,displace:true,pushTo:{q:pz[0],r:pz[1]},capture:false});
          }
        }
      }
    }
    return out;
  }

  function legalSupportTargets(state,zeroId){
    const z=pieceById(state,zeroId);
    if(!z||!z.alive||z.inert||!isNullType(z.type)||z.faction!==state.turnFaction) return [];
    if(z.type==='skiff') return [];                    // пешка Нулей не поддерживает
    if(state.firstMovePawn && !(state.firstMoveDone&&state.firstMoveDone[z.faction]) && hasPawn(state,z.faction)) return [];
    const out=[];
    for(const n of neighbors(z.q,z.r)){
      const tp=pieceAt(state,n[0],n[1]);
      if(tp&&tp.alive&&!tp.inert&&tp.id!==z.id) out.push(tp.id);
    }
    return out;
  }

  // Гибель Линейного: D1 → победа убийцы; D2 → выбывание фракции (фигуры становятся инертными).
  function onLineDestroyed(state,killerFaction,loserFaction){
    if(state.mode.death==='main'){
      state.gameOver=true; state.winner=killerFaction; state.winReason='king';
    } else {
      state.eliminated[loserFaction]=true;
      state.pieces.forEach(p=>{ if(p.faction===loserFaction&&p.alive) p.inert=true; });
      // снимаем щиты на инертных
      state.pieces.forEach(p=>{ if(p.supportTarget){ const t=pieceById(state,p.supportTarget); if(!t||t.inert||!t.alive) p.supportTarget=null; } });
      if(state.eliminated.plus&&state.eliminated.minus){
        // В текущем Длинном режиме недостижимо: после первой гибели Линейного
        // фигуры фракции инертны, а Нули не бьют. Оставлено для книжного режима §14.
        state.gameOver=true; state.winner='zero'; state.winReason='zeros_last';
      }
    }
    // снятие щитов, чьи поддерживающие погибли (не актуально для нулей — нули живы)
    state.pieces.forEach(p=>{ if(p.supportTarget){ const t=pieceById(state,p.supportTarget); if(!t||!t.alive) p.supportTarget=null; } });
  }

  function validateSupports(s){
    s.pieces.forEach(p=>{
      if(!p.supportTarget) return;
      const t=s.pieces.find(x=>x.id===p.supportTarget);
      // §8: щит живёт, пока нуль рядом, цель жива и не инертна
      if(!t||!t.alive||t.inert||dist([p.q,p.r],[t.q,t.r])!==1) p.supportTarget=null;
    });
  }

  function inheritDeaths(state, victimPiece){
    // у нулей, чей щит — victimPiece, снимаем цель поддержки
    state.pieces.forEach(p=>{
      if(p.supportTarget===victimPiece.id) p.supportTarget=null;
    });
  }

  function cloneState(s){
    const ns={
      cells:new Map(),
      pieces:s.pieces.map(p=>({...p})),
      turnFaction:s.turnFaction,
      mode:{...s.mode},
      pawns:s.pawns||0,
      firstMovePawn:!!s.firstMovePawn,
      firstMoveDone:{...(s.firstMoveDone||{plus:false,minus:false,zero:false})},
      cycleDir:s.cycleDir,
      limit:s.limit,
      holdTarget:s.holdTarget||3,
      moveNumber:s.moveNumber,
      hold:{...s.hold},
      lastHolder:s.lastHolder,
      actedBy:{...s.actedBy},
      passLastTarget:{...(s.passLastTarget||{plus:null,minus:null,zero:null})},
      passPrevGiver:s.passPrevGiver||null,
      passRule:s.passRule||'random',
      eliminated:{...s.eliminated},
      gameOver:s.gameOver,
      winner:s.winner,
      winReason:s.winReason,
      log:s.log.map(r=>({...r,from:r.from?{...r.from}:null,to:r.to?{...r.to}:null,capture:r.capture?{...r.capture}:null,extra:r.extra?{...r.extra}:null})),
    };
    for(const [k,v] of s.cells){
      ns.cells.set(k,{q:v.q,r:v.r,kind:v.kind,fieldId:v.fieldId,code:v.code,color:v.color,piece:v.piece});
    }
    return ns;
  }

  const PLUS_SETUP=[
    {type:'line',q:0,r:-5},{type:'battleship',q:0,r:-4},
    {type:'frigate',q:1,r:-5},{type:'bombardier',q:-1,r:-4},
    {type:'corvette',q:2,r:-5},{type:'corvette',q:-2,r:-3},
    {type:'destroyer',q:2,r:-4},{type:'destroyer',q:-2,r:-2},
    {type:'destroyer',q:-1,r:-3},{type:'frigate',q:1,r:-4},
  ];
  const MINUS_SETUP=[
    {type:'line',q:-5,r:5},{type:'battleship',q:-4,r:4},
    {type:'frigate',q:-5,r:4},{type:'bombardier',q:-4,r:5},
    {type:'corvette',q:-5,r:3},{type:'corvette',q:-3,r:5},
    {type:'destroyer',q:-4,r:2},{type:'destroyer',q:-2,r:4},
    {type:'destroyer',q:-3,r:4},{type:'frigate',q:-4,r:3},
  ];
  const ZERO_SETUP=[
    {type:'parliament',q:5,r:0},{type:'parliament',q:4,r:0},
    {type:'engineer',q:4,r:1},{type:'engineer',q:5,r:-1},
    {type:'courier',q:3,r:2},{type:'courier',q:5,r:-2},
    {type:'free',q:2,r:2},{type:'free',q:4,r:-2},
    {type:'free',q:4,r:-1},{type:'saboteur',q:3,r:1},
  ];

  function init(opts){
    const o=opts||{};
    const turnMode=o.turn||'cycle';
    const pawnCount = o.pawns!=null ? Math.max(0, Math.min(5, parseInt(o.pawns,10)||0)) : (o.pieces15?5:0);
    // В «Стрелке» первый ход случайный (лучший баланс после строгих правил).
    // В «Цикле» и «Передаче» первый ход — у Плюсов.
    const startFaction=o.startFaction||(turnMode==='arrow'?FACTIONS[Math.floor(Math.random()*3)]:'plus');
    const state={
      cells:new Map(),
      pieces:[],
      turnFaction:startFaction,
      mode:{turn:turnMode, death:o.death||'main'},
      pawns:pawnCount,
      firstMovePawn:o.firstMovePawn!=null?!!o.firstMovePawn:pawnCount>=5,
      firstMoveDone:{plus:false,minus:false,zero:false},
      cycleDir:o.cycleDir||'forward',       // 'forward' = П→М→Н; 'reverse' = П→Н→М
      limit:pawnCount>=5?LIMIT15:LIMIT,
      holdTarget:Math.max(1,Math.min(9,parseInt(o.holdTarget,10)||3)),
      moveNumber:0,
      hold:{plus:0,minus:0,zero:0},
      lastHolder:null,
      actedBy:{plus:false,minus:false,zero:false},
      passLastTarget:{plus:null,minus:null,zero:null},
      passPrevGiver:null,
      passRule:o.passRule||'random',
      eliminated:{plus:false,minus:false},
      gameOver:false, winner:null, winReason:null,
      log:[],
    };
    function addCell(q,r,kind,fieldId,color){
      state.cells.set(q+','+r,{q,r,kind,fieldId,code:codeMap.get(q+','+r)||'',color,piece:null});
    }
    FIELD_INFO.forEach(f=>patch(f.center[0],f.center[1]).forEach(c=>addCell(c[0],c[1],'field',f.id,f.color)));
    DISPUTED.forEach(d=>addCell(d.q,d.r,'disputed','disputed','#8a8ab0'));
    addCell(0,0,'center','center','#c9a227');
    let pid=0;
    function make(faction,tp){ pid++; const tcode=tp[0].toUpperCase();
      return {id:faction[0].toUpperCase()+'-'+tcode+pid, faction,type:tp, q:0,r:0, alive:true, inert:false, supportTarget:null};
    }
    function place(faction,setup){
      setup.forEach(sp=>{ const p=make(faction,sp.type); p.q=sp.q; p.r=sp.r; state.pieces.push(p);
        const c=state.cells.get(sp.q+','+sp.r); if(c)c.piece=p.id; });
    }
    place('plus',PLUS_SETUP); place('minus',MINUS_SETUP); place('zero',ZERO_SETUP);
    function placePawns(faction,tp){
      const f=FIELD_INFO.find(x=>x.id===faction);
      const cells=state.pawns>=5?PAWN_LOCAL:['C1'];
      cells.forEach(loc=>{
        const code=f.prefix+'-'+loc;
        let cell=null;
        for(const [k,v] of codeMap){ if(v===code){ const [q,r]=k.split(',').map(Number); cell={q,r}; break; } }
        if(!cell) return;
        const c=state.cells.get(cell.q+','+cell.r);
        if(!c||c.piece) return;
        const p=make(faction,tp); p.q=cell.q; p.r=cell.r; state.pieces.push(p); c.piece=p.id;
      });
    }
    if(state.pawns>0){ placePawns('plus','tender'); placePawns('minus','tender'); placePawns('zero','skiff'); }
    return state;
  }

  function isFactionEliminated(s,f){ return f!=='zero' && !!s.eliminated[f]; }

  // Кому можно передать ход: не себе, не выбывшему и не тому же, кому передавал в прошлый раз.
  // Если других активных нет — ограничение снимается, чтобы партия не зависла.
  function legalPassTargets(s,giver){
    if(s.mode.turn!=='pass') return [];
    const others=FACTIONS.filter(f=>f!==giver&&!isFactionEliminated(s,f));
    if(s.passRule==='random') return others;
    if(s.passRule==='back'){
      const prev=s.passPrevGiver||null;
      const fresh=others.filter(f=>f!==prev);
      return fresh.length?fresh:others;
    }
    const last=(s.passLastTarget&&s.passLastTarget[giver])||null;
    const fresh=others.filter(f=>f!==last);
    return fresh.length?fresh:others;
  }

  // Случайный выбор для ИИ/авто (как в тестовом harness): человек выбирает в UI.
  function defaultPassTarget(s,giver){
    const legal=legalPassTargets(s,giver);
    if(!legal.length) return null;
    return legal[Math.floor(Math.random()*legal.length)];
  }

  function validPassTarget(s,giver,target){
    return legalPassTargets(s,giver).indexOf(target)!==-1;
  }

  function nextTurn(s){
    if(s.mode.turn==='pass') return defaultPassTarget(s,s.turnFaction)||s.turnFaction;
    if(s.mode.turn==='arrow'){
      return FACTIONS[Math.floor(Math.random()*3)];
    }
    const idx=FACTIONS.indexOf(s.turnFaction);
    const step=s.cycleDir==='forward'?1:-1;
    return FACTIONS[(idx+step+3)%3];
  }

  function updateHold(s,nextFaction){
    const cp=occupantAt(s);
    const continued=cp&&s.lastHolder===cp.faction;
    if(!continued){
      s.hold={plus:0,minus:0,zero:0};
      s.lastHolder=cp?cp.faction:null;
    }
    // Вход на «Ц» только регистрирует держателя. Тик даёт начало его следующего собственного хода.
    if(continued&&nextFaction===cp.faction){
      const f=cp.faction;
      s.hold[f]+=1;
      if(s.hold[f]>=(s.holdTarget||3)){ s.gameOver=true; s.winner=f; s.winReason='hold'; }
    }
  }

  function advanceTurn(s,prevFaction,record,passTo){
    s.actedBy[prevFaction]=true;
    s.moveNumber++;
    if(!s.gameOver){
      if(s.mode.turn==='pass'){
        if(!s.passLastTarget) s.passLastTarget={plus:null,minus:null,zero:null};
        if(s.passPrevGiver===undefined) s.passPrevGiver=null;
        const target=(passTo&&validPassTarget(s,prevFaction,passTo))?passTo:defaultPassTarget(s,prevFaction);
        if(target){
          s.turnFaction=target;
          s.passLastTarget[prevFaction]=target;
          s.passPrevGiver=prevFaction;
          if(record){ record.passFrom=prevFaction; record.passTo=target; }
        } else {
          s.turnFaction=nextTurn(s);
        }
      } else {
        s.turnFaction=nextTurn(s);
      }
      updateHold(s,s.turnFaction);
    }
    if(!s.gameOver&&s.moveNumber>=s.limit){
      const cp=occupantAt(s);
      if(cp){ s.gameOver=true; s.winner=cp.faction; s.winReason='limit'; }
      else { s.gameOver=true; s.winner=null; s.winReason='draw'; }
    }
    if(record) s.log.push(record);
  }

  function applyMove(state,pieceId,toQ,toR,passTo){
    const s=cloneState(state);
    if(s.mode.turn==='pass'&&passTo&&!validPassTarget(s,s.turnFaction,passTo)) return {state:s,record:null,error:'illegal pass target'};
    const p=s.pieces.find(x=>x.id===pieceId&&x.alive&&!x.inert);
    if(!p) return {state:s,record:null,error:'piece not found'};
    if(p.faction!==s.turnFaction) return {state:s,record:null,error:'not your turn'};
    if(s.firstMovePawn && !(s.firstMoveDone&&s.firstMoveDone[p.faction]) && hasPawn(s,p.faction) && !isPawnType(p.type)) return {state:s,record:null,error:'first move must be pawn'};
    const moves=legalMoves(s,pieceId);
    const mv=moves.find(m=>m.q===toQ&&m.r===toR);
    if(!mv) return {state:s,record:null,error:'illegal move'};
    const from={q:p.q,r:p.r};
    let captureRecord=null;
    const target=pieceAt(s,toQ,toR);
    if(mv.displace){
      // Оттеснение: атакующий остаётся на месте, нуль сдвигается на mv.pushTo (§7).
      if(isNullType(p.type)) return {state:s,record:null,error:'zeroes cannot displace'};
      const zero=pieceAt(s,toQ,toR);
      if(!zero||!isNeutralZero(zero)) return {state:s,record:null,error:'bad displace cell'};
      const zc=s.cells.get(toQ+','+toR);
      if(zc) zc.piece=null;                       // старая клетка нуля
      zero.q=mv.pushTo.q; zero.r=mv.pushTo.r;
      const c2=s.cells.get(mv.pushTo.q+','+mv.pushTo.r);
      if(c2)c2.piece=zero.id;
      if((s.pawns||0)>0 && isPawnType(p.type)) s.firstMoveDone[p.faction]=true;
      validateSupports(s);
      const record={kind:'displace',moveNumber:s.moveNumber+1,faction:p.faction,pieceId,
        from:{q:toQ,r:toR},to:{q:zero.q,r:zero.r},pushTo:{q:zero.q,r:zero.r},
        attackerId:p.id,attackerPos:{q:p.q,r:p.r},
        holdAfter:{...s.hold},extra:null,capture:null};
      advanceTurn(s,p.faction,record,passTo);
      return {state:s,record,error:null};
    }
    if(target&&target.faction!==p.faction){
      target.alive=false;
      inheritDeaths(s,target);
      if(isNullType(target.type)){
        // снятие нуля: счётчик инертности? нет — просто удаляем
        captureRecord={pieceId:target.id,faction:target.faction,type:target.type};
      } else {
        captureRecord={pieceId:target.id,faction:target.faction,type:target.type};
        if(target.type==='line') onLineDestroyed(s,p.faction,target.faction);
      }
    }
    s.cells.set(from.q+','+from.r,{...s.cells.get(from.q+','+from.r),piece:null});
    p.q=toQ; p.r=toR;
    const c=s.cells.get(toQ+','+toR);
    if(c)c.piece=p.id;
    if((s.pawns||0)>0 && isPawnType(p.type)) s.firstMoveDone[p.faction]=true;
    validateSupports(s);
    const record={kind:'move',moveNumber:s.moveNumber+1,faction:p.faction,pieceId,from,to:{q:toQ,r:toR},capture:captureRecord,extra:null,holdAfter:{...s.hold}};
    advanceTurn(s,p.faction,record,passTo);
    return {state:s,record,error:null};
  }

  function applySupport(state,zeroId,targetId,passTo){
    const s=cloneState(state);
    if(s.mode.turn==='pass'&&passTo&&!validPassTarget(s,s.turnFaction,passTo)) return {state:s,record:null,error:'illegal pass target'};
    const z=s.pieces.find(x=>x.id===zeroId&&x.alive&&!x.inert);
    if(!z) return {state:s,record:null,error:'zero not found'};
    if(z.faction!==s.turnFaction) return {state:s,record:null,error:'not your turn'};
    if(z.type==='skiff') return {state:s,record:null,error:'skiff cannot support'};
    if(s.firstMovePawn && !(s.firstMoveDone&&s.firstMoveDone[z.faction]) && hasPawn(s,z.faction)) return {state:s,record:null,error:'first move must be pawn'};
    if(!isNullType(z.type)) return {state:s,record:null,error:'not a zero'};
    if(targetId!==null){
      const t=s.pieces.find(x=>x.id===targetId&&x.alive&&!x.inert);
      if(!t||dist([z.q,z.r],[t.q,t.r])!==1||t.id===z.id) return {state:s,record:null,error:'bad target'};
    }
    z.supportTarget=targetId;
    const record={kind:'support',moveNumber:s.moveNumber+1,faction:z.faction,pieceId:zeroId,targetId,from:null,to:null,capture:null,extra:null,holdAfter:{...s.hold}};
    advanceTurn(s,z.faction,record,passTo);
    return {state:s,record,error:null};
  }

  function applyDisplace(state,attackerId,zeroQ,zeroR,passTo){
    const s=cloneState(state);
    if(s.mode.turn==='pass'&&passTo&&!validPassTarget(s,s.turnFaction,passTo)) return {state:s,record:null,error:'illegal pass target'};
    const a=s.pieces.find(x=>x.id===attackerId&&x.alive&&!x.inert);
    const z=pieceAt(s,zeroQ,zeroR);
    if(!a||isNullType(a.type)||!z||!isNeutralZero(z)||a.faction!==s.turnFaction) return {state:s,record:null,error:'bad displace'};
    if(dist([a.q,a.r],[zeroQ,zeroR])!==1) return {state:s,record:null,error:'not adjacent'};
    if(s.firstMovePawn && !(s.firstMoveDone&&s.firstMoveDone[a.faction]) && hasPawn(s,a.faction)) return {state:s,record:null,error:'first move must be pawn'};
    const dir=[zeroQ-a.q, zeroR-a.r];
    const pz=[zeroQ+dir[0], zeroR+dir[1]];
    if(!isValidCell(s,pz[0],pz[1])||pieceAt(s,pz[0],pz[1])||(pz[0]===0&&pz[1]===0)||GATE_SET.has(pz[0]+','+pz[1])||DISPUTED_SET.has(pz[0]+','+pz[1])) return {state:s,record:null,error:'bad push'};
    s.cells.set(zeroQ+','+zeroR,{...s.cells.get(zeroQ+','+zeroR),piece:null});
    z.q=pz[0]; z.r=pz[1];
    const c2=s.cells.get(pz[0]+','+pz[1]);
    if(c2)c2.piece=z.id;
    if((s.pawns||0)>0 && isPawnType(a.type)) s.firstMoveDone[a.faction]=true;
    validateSupports(s);
    const zeroQ2=z.q, zeroR2=z.r;
    const record={kind:'displace',moveNumber:s.moveNumber+1,faction:a.faction,pieceId:attackerId,
      from:{q:zeroQ,r:zeroR},to:{q:zeroQ2,r:zeroR2},pushTo:{q:zeroQ2,r:zeroR2},
      attackerId:a.id,attackerPos:{q:a.q,r:a.r},
      capture:null,extra:null,holdAfter:{...s.hold}};
    advanceTurn(s,a.faction,record,passTo);
    return {state:s,record,error:null};
  }

  function skipTurn(state,passTo){
    const s=cloneState(state);
    if(s.mode.turn==='pass'&&passTo&&!validPassTarget(s,s.turnFaction,passTo)) return {state:s,record:null,error:'illegal pass target'};
    const prev=s.turnFaction;
    const record={kind:'skip',moveNumber:s.moveNumber+1,faction:prev,holdAfter:{...s.hold}};
    advanceTurn(s,prev,record,passTo);
    return {state:s,record};
  }

  function clone(s){return cloneState(s);}

  window.ElioEngine={
    FACTIONS, FACTION_NAME, FACTION_COLOR, PIECE_VALUE, TYPE_NAME,
    isHeavy, isNullType, isPawnType, isNeutralZero, isDeclaredZero, isOnGateOrCenter,
    FIELD_CENTERS, FIELD_INFO, DISPUTED, INNER_GATES, GATE_SET, DISPUTED_SET, NB,
    PAWN_LOCAL, LIMIT15,
    codeMap, init, clone,
    neighbors, isValidCell:(s,q,r)=>isValidCell(s,q,r), cellAt, pieceAt, occupantAt,
    protLevel, canCapture, canCaptureZero, zeroVulnerability, legalMoves, legalSupportTargets,
    legalPassTargets, defaultPassTarget, validPassTarget,
    applyMove, applySupport, applyDisplace, skipTurn,
    cellKey:(q,r)=>q+','+r,
    dist, patch, LIMIT,
    stateToJSON:(s)=>JSON.parse(JSON.stringify({pieces:s.pieces,turnFaction:s.turnFaction,mode:s.mode,pawns:s.pawns||0,firstMovePawn:!!s.firstMovePawn,firstMoveDone:s.firstMoveDone,cycleDir:s.cycleDir,limit:s.limit,holdTarget:s.holdTarget||3,moveNumber:s.moveNumber,hold:s.hold,lastHolder:s.lastHolder,actedBy:s.actedBy,passLastTarget:s.passLastTarget,passPrevGiver:s.passPrevGiver,passRule:s.passRule,eliminated:s.eliminated,gameOver:s.gameOver,winner:s.winner,winReason:s.winReason,log:s.log})),
    fromJSON:(j)=>{ const s=init({turn:j.mode.turn,death:j.mode.death,cycleDir:j.cycleDir,passRule:j.passRule,pawns:j.pawns!=null?j.pawns:(j.pieces15?5:0),firstMovePawn:j.firstMovePawn,holdTarget:j.holdTarget}); s.pieces=[];j.pieces.forEach(p=>{const np={...p}; s.pieces.push(np); const c=s.cells.get(np.q+','+np.r); if(c)c.piece=np.id;});
      s.turnFaction=j.turnFaction; s.moveNumber=j.moveNumber; s.hold={...j.hold}; s.lastHolder=j.lastHolder;
      s.actedBy={...j.actedBy}; s.passLastTarget={...(j.passLastTarget||{plus:null,minus:null,zero:null})};
      s.passPrevGiver=j.passPrevGiver||null; s.passRule=j.passRule||'random';
      s.pawns=j.pawns!=null?j.pawns:(j.pieces15?5:0); s.firstMovePawn=!!j.firstMovePawn; s.firstMoveDone={...(j.firstMoveDone||{plus:false,minus:false,zero:false})};
      s.eliminated={...j.eliminated}; s.gameOver=j.gameOver; s.winner=j.winner; s.winReason=j.winReason; s.log=j.log.slice(); return s; },
  };
})();
