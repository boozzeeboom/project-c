// Elio AI v1.3c — работает с нейтралитетом/заявкой, поддержкой 1→1, режимами хода/смерти, лимитом 90.
// v1.3c: запрет пустых поддержек (повтор той же цели), анти-осцилляция (не возвращать фигуру назад),
// в Длинном режиме ИИ не бросает уже накопленный «Ц» (hold ≥ 1) ради убийства Линейного: это выбывание, не победа.
(function(){
  const E = window.ElioEngine;

  function pieceVal(p){
    const v={line:7,battleship:6,bombardier:4,heavy:5,frigate:3,corvette:2,destroyer:1,tender:1,skiff:1,parliament:4,free:2,courier:1.5,engineer:2.5,saboteur:2};
    return v[p.type]||1;
  }

  // Счёт: позиция, удержание, щиты, гибель Линейного, угроза потери фигур.
  function scoreState(state,faction){
    let s=0;    // материал
    for(const p of state.pieces){
      if(!p.alive||!p.type)continue;
      if(p.faction===faction) s+=pieceVal(p)*3;
      else if(p.faction!=='zero'&&faction!=='zero') s-=pieceVal(p)*3;
      // нули со стороны военной фракции: щиты — не материал
    }
    // удержание «Ц» — счёт по фракции (правило полного круга)
    s += (state.hold[faction]||0)*10;
    for(const o of ['plus','minus','zero']){
      if(o===faction)continue;
      s -= (state.hold[o]||0)*8;
    }
    // держатель «Ц» сейчас; критично: защита держателя
    const cp=E.occupantAt(state);
    if(cp){
      if(cp.faction===faction){
        const lv=E.protLevel(state,cp);
        s += E.isNullType(cp.type) ? (lv>=1?32:14) : (lv>=1?35:16);
      } else if(cp.faction==='zero'&&faction!=='zero'){
        s -= 22;
      } else if(cp.faction!=='zero'&&faction!=='zero'){
        s -= 14;
      } else {
        s += 8;
      }
    }
    // поддержка есть? (для военных ещё щит на себя/Линейного)
    for(const p of state.pieces.filter(pp=>pp.alive&&pp.faction===faction&&!E.isNullType(pp.type))){
      const lv=E.protLevel(state,p);
      s += lv===2?12:(lv===1?5:0);
    }
    // Нули на «Ц» своей фракции и их щиты (для Нулей)
    for(const zb of state.pieces.filter(z=>z.alive&&E.isNullType(z.type)&&z.q===0&&z.r===0)){
      if(zb.faction===faction) s+=30;
      for(const n of E.neighbors(0,0)){
        const sg=E.pieceAt(state,n[0],n[1]);
        if(sg&&E.isNullType(sg.type)&&sg.supportTarget===zb.id){
          s+= sg.faction===faction?25:-25;
        }
      }
    }
    // === дальнобойные угрозы королю (v1.3) ===
    for(const f of ['plus','minus']){
      if(f===faction) continue;
      const enemyLine=state.pieces.find(p=>p.alive&&p.faction===f&&p.type==='line');
      if(!enemyLine) continue;
      for(const p of state.pieces.filter(pp=>pp.alive&&pp.faction===faction&&(pp.type==='bombardier'||pp.type==='battleship'))){
        const dq=enemyLine.q-p.q, dr=enemyLine.r-p.r;
        const aligned=(dq===0||dr===0||dq+dr===0);
        const d=E.dist([p.q,p.r],[enemyLine.q,enemyLine.r]);
        if(p.type==='bombardier'&&aligned&&d>=2&&d<=3) s+=120;
        if(p.type==='bombardier') s+=Math.max(0,8-d)*3;
        if(p.type==='battleship'&&aligned) s+=30;
        if(p.type==='battleship') s+=Math.max(0,8-d)*2;
      }
      // чужой бомбардир угрожает нашему королю
      const ourLine=state.pieces.find(p=>p.alive&&p.faction===faction&&p.type==='line');
      if(ourLine&&E.protLevel(state,ourLine)===0){
        for(const ep of state.pieces.filter(pp=>pp.alive&&pp.faction===f&&pp.type==='bombardier')){
          const dq=ourLine.q-ep.q, dr=ourLine.r-ep.r;
          const aligned=(dq===0||dr===0||dq+dr===0);
          const d=E.dist([ep.q,ep.r],[ourLine.q,ourLine.r]);
          if(aligned&&d>=2&&d<=3) s-=100;
        }
      }
    }
    // гибель Линейного (D1 – «убил=победил»; D2 – выбывание)
    if(state.mode.death==='main'){
      for(const f of ['plus','minus']){
        if(f===faction)continue;
        const dead = !state.pieces.some(p=>p.alive&&p.faction===f&&p.type==='line');
        if(dead) s+=250;    // наш выигрыш по «королю» (D1)
      }
    }
    // победа/поражение — бонус от самого исхода
    if(state.gameOver){
      if(state.winner===faction) s+=1000;
      else if(state.winner) s -= 700;         // проигрыш; ничья = небольшая пессимизация тоже
    }
    // нас могут взять: по фигурам Faction, core threat penalty
    let threats=0;
    for(const p of state.pieces){
      if(!p.alive||p.faction!==faction)continue;
      // посчитать, сколько раз мы текущим state уже уязвимы: дешевле один раз считать после apply — тут мы уже после него
      // -> оставляем только для будущих, не для текущего
    }
    // нули рядом с «Ц» — стимул у Zero идти туда
    if(faction==='zero'){
      const onGate=['0,-1','-1,1','1,0'];
      for(const g of onGate){
        const arr=g.split(',').map(Number);
        const p=E.pieceAt(state,arr[0],arr[1]);
        if(p&&p.faction==='zero') s+=15;
      }
    }
    // наши фигуры рядом с Ц — чуть больше ценности военным (у Нулей позиция важнее центра как такового)
    if(faction!=='zero'){
      for(const p of state.pieces.filter(pp=>pp.alive&&pp.faction===faction&&!E.isNullType(pp.type))){
        const d=E.dist([p.q,p.r],[0,0]);
        if(d<4) s+=(4-d)*1;      // ближе к центру — чуть лучше
      }
    }
    // если нуль поддерживает военную фигуру — парламент «коронер»: делает войну управляемой
    for(const p of state.pieces.filter(pp=>pp.alive&&pp.faction===faction&&E.isNullType(pp.type))){
      if(p.supportTarget){
        const t=E.pieceAt(state,(state.pieces.find(x=>x.id===p.supportTarget)||{}).q,(state.pieces.find(x=>x.id===p.supportTarget)||{}).r);
        if(t&&!E.isNullType(t.type)) s+=12;
      }
    }
    return s;
  }

  // Является ли ход опасным (после того как мы совершили ход, на ту же клетку может зайти соперник без выгоды?)
  function afterAction(state, act){
    if(act.type==='move') return E.applyMove(state, act.pieceId, act.q, act.r);
    if(act.type==='support') return E.applySupport(state, act.zeroId, act.targetId);
    return {state:E.clone(state), error:'unknown'};
  }

  function allCandidates(state,faction){
    const out=[];
    for(const p of state.pieces.filter(x=>x.alive&&!x.inert&&x.faction===faction&&x.type!==undefined)){
      if(E.isNullType(p.type)){
        // варианты «движение»
        for(const mv of E.legalMoves(state,p.id)) out.push({type:'move',pieceId:p.id,q:mv.q,r:mv.r,capture:mv.capture,displace:mv.displace});
        // варианты «поддержка» (смена/сброс цели); повтор той же цели — пустая трата хода
        for(const tId of E.legalSupportTargets(state,p.id)){
          if((p.supportTarget||null)===(tId||null)) continue;
          out.push({type:'support',zeroId:p.id,targetId:tId});
        }
        if(p.supportTarget) out.push({type:'support',zeroId:p.id,targetId:null});
      } else {
        for(const mv of E.legalMoves(state,p.id)) out.push({type:'move',pieceId:p.id,q:mv.q,r:mv.r,capture:mv.capture,displace:mv.displace});
      }
    }
    return out;
  }

  // Последний ход этой фигуры — чтобы не гонять её туда-обратно.
  function lastMoveOf(state,pieceId){
    for(let i=state.log.length-1;i>=0;i--){
      const r=state.log[i];
      if(r.kind==='move'&&r.pieceId===pieceId) return r;
    }
    return null;
  }

  function chooseMove(state,faction){
    if(state.gameOver) return {};
    if(state.turnFaction!==faction) return {};
    const cands=allCandidates(state,faction);
    if(!cands.length){
      const sk=E.skipTurn(state);
      return {type:'skip',state:sk.state};
    }
    let best=null,bestSc=-Infinity;
    for(const act of cands){
      const sim=E.clone(state);
      const r=afterAction(sim,act);
      if(r.error)continue;
      // считаем давление: сколько наших фигур после хода уязвимо
      const sc0=scoreState(r.state,faction);
      const after=r.state;
      // анти-осцилляция: не возвращать фигуру на клетку, с которой она только что ушла
      let oscPenalty=0;
      if(act.type==='move'&&!act.capture&&!(act.q===0&&act.r===0)&&!E.GATE_SET.has(act.q+','+act.r)){
        const p0=state.pieces.find(x=>x.id===act.pieceId);
        const lm=lastMoveOf(state,act.pieceId);
        if(p0&&lm&&lm.from&&lm.to&&lm.to.q===p0.q&&lm.to.r===p0.r&&lm.from.q===act.q&&lm.from.r===act.r) oscPenalty=5;
      }
      const enemyFactions=faction==='zero'?['plus','minus']:(faction==='plus'?['minus']:['plus']);
      let dangerCount=0;
      for(const f of enemyFactions){
        const s2=E.clone(after);
        s2.turnFaction=f;
        const movable=s2.pieces.filter(x=>x.alive&&!x.inert&&x.faction===f);
        for(const ep of movable){
          const ms=E.legalMoves(s2,ep.id);
          for(const m of ms){
            if(!m.capture)continue;
            const victim=E.pieceAt(s2,m.q,m.r);
            if(victim&&victim.faction===faction){
              dangerCount += victim.type==='line'?20:pieceVal(victim)*1.5;   // король сильно дороже
            }
          }
        }
      }
      const sc=sc0-dangerCount*2-oscPenalty;
      const scJ=sc + Math.random()*1.5;    // лёгкый tie-break, чтобы партии различались
      if(scJ>bestSc){bestSc=scJ;best=act;}
    }
    if(!best){ const sk=E.skipTurn(state); return {type:'skip',state:sk.state}; }
    const final=afterAction(E.clone(state),best);
    return {type:best.type,state:final.state,act:best};
  }

  function chooseMoveAggressive(state,faction){
    // военные: приоритет захвата, особенно чужой «король» (D1) или заявленный нуль-щит
    if(state.gameOver||state.turnFaction!==faction) return {};
    if(faction==='zero') return chooseMove(state,faction);
    const cands=allCandidates(state,faction).filter(c=>c.type==='move');
    // приоритет 1: победная смена «короля» (в Основном — всегда;
    // в Длинном — не бросать уже накопленный «Ц» при hold ≥ 1)
    const cp=E.occupantAt(state);
    const onCenter=cp&&cp.faction===faction;
    if(state.mode.death!=='long'||!onCenter||(state.hold[faction]||0)>=1){
      const win=cands.find(c=>{
        const t=E.pieceAt(state,c.q,c.r);
        return t&&t.type==='line';
      });
      if(win){ const r=afterAction(E.clone(state),win); if(!r.error) return {type:'move',state:r.state,act:win}; }
    }
    // приоритет 2: смена чужого щитового нуля (ломает поддержку 2)
    const shield=cands.find(c=>{
      const t=E.pieceAt(state,c.q,c.r);
      return t&&E.isNullType(t.type)&&t.supportTarget&&(t.supportTarget!==null);
    });
    if(shield){ const r=afterAction(E.clone(state),shield); if(!r.error) return {type:'move',state:r.state,act:shield}; }
    return chooseMove(state,faction);
  }

  window.ElioAI={
    chooseMove:(state,faction)=>chooseMove(state,faction),
    chooseMoveAggressive:(state,faction)=>chooseMoveAggressive(state,faction),
    scoreState:(state,faction)=>scoreState(state,faction),
    pieceVal,
  };
})();
