(function(){const t=document.createElement("link").relList;if(t&&t.supports&&t.supports("modulepreload"))return;for(const i of document.querySelectorAll('link[rel="modulepreload"]'))n(i);new MutationObserver(i=>{for(const r of i)if(r.type==="childList")for(const a of r.addedNodes)a.tagName==="LINK"&&a.rel==="modulepreload"&&n(a)}).observe(document,{childList:!0,subtree:!0});function e(i){const r={};return i.integrity&&(r.integrity=i.integrity),i.referrerPolicy&&(r.referrerPolicy=i.referrerPolicy),i.crossOrigin==="use-credentials"?r.credentials="include":i.crossOrigin==="anonymous"?r.credentials="omit":r.credentials="same-origin",r}function n(i){if(i.ep)return;i.ep=!0;const r=e(i);fetch(i.href,r)}})();const Ma="180",xn={ROTATE:0,DOLLY:1,PAN:2},Pn={ROTATE:0,PAN:1,DOLLY_PAN:2,DOLLY_ROTATE:3},ac=0,Za=1,oc=2,pl=1,ml=2,_n=3,Dn=0,Ie=1,sn=2,vn=0,xi=1,Pr=2,$a=3,Ja=4,lc=5,Vn=100,cc=101,hc=102,uc=103,dc=104,fc=200,pc=201,mc=202,gc=203,Lr=204,Dr=205,_c=206,xc=207,vc=208,yc=209,Mc=210,Sc=211,Ec=212,bc=213,Tc=214,Ir=0,Ur=1,Nr=2,yi=3,Fr=4,Or=5,Br=6,zr=7,gl=0,wc=1,Ac=2,Ln=0,_l=1,xl=2,vl=3,Sa=4,yl=5,Ml=6,Sl=7,El=300,Mi=301,Si=302,kr=303,Hr=304,Vs=306,Vr=1e3,Wn=1001,Gr=1002,ze=1003,Cc=1004,es=1005,rn=1006,Ys=1007,Xn=1008,ln=1009,bl=1010,Tl=1011,Wi=1012,Ea=1013,jn=1014,an=1015,yn=1016,ba=1017,Ta=1018,Xi=1020,wl=35902,Al=35899,Cl=1021,Rl=1022,Ze=1023,qi=1026,Yi=1027,wa=1028,Aa=1029,Pl=1030,Ca=1031,Ra=1033,Rs=33776,Ps=33777,Ls=33778,Ds=33779,Wr=35840,Xr=35841,qr=35842,Yr=35843,jr=36196,Kr=37492,Zr=37496,$r=37808,Jr=37809,Qr=37810,ta=37811,ea=37812,na=37813,ia=37814,sa=37815,ra=37816,aa=37817,oa=37818,la=37819,ca=37820,ha=37821,ua=36492,da=36494,fa=36495,pa=36283,ma=36284,ga=36285,_a=36286,Rc=3200,Pc=3201,Ll=0,Lc=1,Rn="",Be="srgb",Ei="srgb-linear",Fs="linear",jt="srgb",ei=7680,Qa=519,Dc=512,Ic=513,Uc=514,Dl=515,Nc=516,Fc=517,Oc=518,Bc=519,to=35044,eo="300 es",on=2e3,Os=2001;class Zn{addEventListener(t,e){this._listeners===void 0&&(this._listeners={});const n=this._listeners;n[t]===void 0&&(n[t]=[]),n[t].indexOf(e)===-1&&n[t].push(e)}hasEventListener(t,e){const n=this._listeners;return n===void 0?!1:n[t]!==void 0&&n[t].indexOf(e)!==-1}removeEventListener(t,e){const n=this._listeners;if(n===void 0)return;const i=n[t];if(i!==void 0){const r=i.indexOf(e);r!==-1&&i.splice(r,1)}}dispatchEvent(t){const e=this._listeners;if(e===void 0)return;const n=e[t.type];if(n!==void 0){t.target=this;const i=n.slice(0);for(let r=0,a=i.length;r<a;r++)i[r].call(this,t);t.target=null}}}const Ee=["00","01","02","03","04","05","06","07","08","09","0a","0b","0c","0d","0e","0f","10","11","12","13","14","15","16","17","18","19","1a","1b","1c","1d","1e","1f","20","21","22","23","24","25","26","27","28","29","2a","2b","2c","2d","2e","2f","30","31","32","33","34","35","36","37","38","39","3a","3b","3c","3d","3e","3f","40","41","42","43","44","45","46","47","48","49","4a","4b","4c","4d","4e","4f","50","51","52","53","54","55","56","57","58","59","5a","5b","5c","5d","5e","5f","60","61","62","63","64","65","66","67","68","69","6a","6b","6c","6d","6e","6f","70","71","72","73","74","75","76","77","78","79","7a","7b","7c","7d","7e","7f","80","81","82","83","84","85","86","87","88","89","8a","8b","8c","8d","8e","8f","90","91","92","93","94","95","96","97","98","99","9a","9b","9c","9d","9e","9f","a0","a1","a2","a3","a4","a5","a6","a7","a8","a9","aa","ab","ac","ad","ae","af","b0","b1","b2","b3","b4","b5","b6","b7","b8","b9","ba","bb","bc","bd","be","bf","c0","c1","c2","c3","c4","c5","c6","c7","c8","c9","ca","cb","cc","cd","ce","cf","d0","d1","d2","d3","d4","d5","d6","d7","d8","d9","da","db","dc","dd","de","df","e0","e1","e2","e3","e4","e5","e6","e7","e8","e9","ea","eb","ec","ed","ee","ef","f0","f1","f2","f3","f4","f5","f6","f7","f8","f9","fa","fb","fc","fd","fe","ff"];let no=1234567;const ki=Math.PI/180,ji=180/Math.PI;function wi(){const s=Math.random()*4294967295|0,t=Math.random()*4294967295|0,e=Math.random()*4294967295|0,n=Math.random()*4294967295|0;return(Ee[s&255]+Ee[s>>8&255]+Ee[s>>16&255]+Ee[s>>24&255]+"-"+Ee[t&255]+Ee[t>>8&255]+"-"+Ee[t>>16&15|64]+Ee[t>>24&255]+"-"+Ee[e&63|128]+Ee[e>>8&255]+"-"+Ee[e>>16&255]+Ee[e>>24&255]+Ee[n&255]+Ee[n>>8&255]+Ee[n>>16&255]+Ee[n>>24&255]).toLowerCase()}function zt(s,t,e){return Math.max(t,Math.min(e,s))}function Pa(s,t){return(s%t+t)%t}function zc(s,t,e,n,i){return n+(s-t)*(i-n)/(e-t)}function kc(s,t,e){return s!==t?(e-s)/(t-s):0}function Hi(s,t,e){return(1-e)*s+e*t}function Hc(s,t,e,n){return Hi(s,t,1-Math.exp(-e*n))}function Vc(s,t=1){return t-Math.abs(Pa(s,t*2)-t)}function Gc(s,t,e){return s<=t?0:s>=e?1:(s=(s-t)/(e-t),s*s*(3-2*s))}function Wc(s,t,e){return s<=t?0:s>=e?1:(s=(s-t)/(e-t),s*s*s*(s*(s*6-15)+10))}function Xc(s,t){return s+Math.floor(Math.random()*(t-s+1))}function qc(s,t){return s+Math.random()*(t-s)}function Yc(s){return s*(.5-Math.random())}function jc(s){s!==void 0&&(no=s);let t=no+=1831565813;return t=Math.imul(t^t>>>15,t|1),t^=t+Math.imul(t^t>>>7,t|61),((t^t>>>14)>>>0)/4294967296}function Kc(s){return s*ki}function Zc(s){return s*ji}function $c(s){return(s&s-1)===0&&s!==0}function Jc(s){return Math.pow(2,Math.ceil(Math.log(s)/Math.LN2))}function Qc(s){return Math.pow(2,Math.floor(Math.log(s)/Math.LN2))}function th(s,t,e,n,i){const r=Math.cos,a=Math.sin,o=r(e/2),l=a(e/2),c=r((t+n)/2),h=a((t+n)/2),d=r((t-n)/2),u=a((t-n)/2),p=r((n-t)/2),g=a((n-t)/2);switch(i){case"XYX":s.set(o*h,l*d,l*u,o*c);break;case"YZY":s.set(l*u,o*h,l*d,o*c);break;case"ZXZ":s.set(l*d,l*u,o*h,o*c);break;case"XZX":s.set(o*h,l*g,l*p,o*c);break;case"YXY":s.set(l*p,o*h,l*g,o*c);break;case"ZYZ":s.set(l*g,l*p,o*h,o*c);break;default:console.warn("THREE.MathUtils: .setQuaternionFromProperEuler() encountered an unknown order: "+i)}}function gi(s,t){switch(t.constructor){case Float32Array:return s;case Uint32Array:return s/4294967295;case Uint16Array:return s/65535;case Uint8Array:return s/255;case Int32Array:return Math.max(s/2147483647,-1);case Int16Array:return Math.max(s/32767,-1);case Int8Array:return Math.max(s/127,-1);default:throw new Error("Invalid component type.")}}function we(s,t){switch(t.constructor){case Float32Array:return s;case Uint32Array:return Math.round(s*4294967295);case Uint16Array:return Math.round(s*65535);case Uint8Array:return Math.round(s*255);case Int32Array:return Math.round(s*2147483647);case Int16Array:return Math.round(s*32767);case Int8Array:return Math.round(s*127);default:throw new Error("Invalid component type.")}}const Is={DEG2RAD:ki,RAD2DEG:ji,generateUUID:wi,clamp:zt,euclideanModulo:Pa,mapLinear:zc,inverseLerp:kc,lerp:Hi,damp:Hc,pingpong:Vc,smoothstep:Gc,smootherstep:Wc,randInt:Xc,randFloat:qc,randFloatSpread:Yc,seededRandom:jc,degToRad:Kc,radToDeg:Zc,isPowerOfTwo:$c,ceilPowerOfTwo:Jc,floorPowerOfTwo:Qc,setQuaternionFromProperEuler:th,normalize:we,denormalize:gi};class dt{constructor(t=0,e=0){dt.prototype.isVector2=!0,this.x=t,this.y=e}get width(){return this.x}set width(t){this.x=t}get height(){return this.y}set height(t){this.y=t}set(t,e){return this.x=t,this.y=e,this}setScalar(t){return this.x=t,this.y=t,this}setX(t){return this.x=t,this}setY(t){return this.y=t,this}setComponent(t,e){switch(t){case 0:this.x=e;break;case 1:this.y=e;break;default:throw new Error("index is out of range: "+t)}return this}getComponent(t){switch(t){case 0:return this.x;case 1:return this.y;default:throw new Error("index is out of range: "+t)}}clone(){return new this.constructor(this.x,this.y)}copy(t){return this.x=t.x,this.y=t.y,this}add(t){return this.x+=t.x,this.y+=t.y,this}addScalar(t){return this.x+=t,this.y+=t,this}addVectors(t,e){return this.x=t.x+e.x,this.y=t.y+e.y,this}addScaledVector(t,e){return this.x+=t.x*e,this.y+=t.y*e,this}sub(t){return this.x-=t.x,this.y-=t.y,this}subScalar(t){return this.x-=t,this.y-=t,this}subVectors(t,e){return this.x=t.x-e.x,this.y=t.y-e.y,this}multiply(t){return this.x*=t.x,this.y*=t.y,this}multiplyScalar(t){return this.x*=t,this.y*=t,this}divide(t){return this.x/=t.x,this.y/=t.y,this}divideScalar(t){return this.multiplyScalar(1/t)}applyMatrix3(t){const e=this.x,n=this.y,i=t.elements;return this.x=i[0]*e+i[3]*n+i[6],this.y=i[1]*e+i[4]*n+i[7],this}min(t){return this.x=Math.min(this.x,t.x),this.y=Math.min(this.y,t.y),this}max(t){return this.x=Math.max(this.x,t.x),this.y=Math.max(this.y,t.y),this}clamp(t,e){return this.x=zt(this.x,t.x,e.x),this.y=zt(this.y,t.y,e.y),this}clampScalar(t,e){return this.x=zt(this.x,t,e),this.y=zt(this.y,t,e),this}clampLength(t,e){const n=this.length();return this.divideScalar(n||1).multiplyScalar(zt(n,t,e))}floor(){return this.x=Math.floor(this.x),this.y=Math.floor(this.y),this}ceil(){return this.x=Math.ceil(this.x),this.y=Math.ceil(this.y),this}round(){return this.x=Math.round(this.x),this.y=Math.round(this.y),this}roundToZero(){return this.x=Math.trunc(this.x),this.y=Math.trunc(this.y),this}negate(){return this.x=-this.x,this.y=-this.y,this}dot(t){return this.x*t.x+this.y*t.y}cross(t){return this.x*t.y-this.y*t.x}lengthSq(){return this.x*this.x+this.y*this.y}length(){return Math.sqrt(this.x*this.x+this.y*this.y)}manhattanLength(){return Math.abs(this.x)+Math.abs(this.y)}normalize(){return this.divideScalar(this.length()||1)}angle(){return Math.atan2(-this.y,-this.x)+Math.PI}angleTo(t){const e=Math.sqrt(this.lengthSq()*t.lengthSq());if(e===0)return Math.PI/2;const n=this.dot(t)/e;return Math.acos(zt(n,-1,1))}distanceTo(t){return Math.sqrt(this.distanceToSquared(t))}distanceToSquared(t){const e=this.x-t.x,n=this.y-t.y;return e*e+n*n}manhattanDistanceTo(t){return Math.abs(this.x-t.x)+Math.abs(this.y-t.y)}setLength(t){return this.normalize().multiplyScalar(t)}lerp(t,e){return this.x+=(t.x-this.x)*e,this.y+=(t.y-this.y)*e,this}lerpVectors(t,e,n){return this.x=t.x+(e.x-t.x)*n,this.y=t.y+(e.y-t.y)*n,this}equals(t){return t.x===this.x&&t.y===this.y}fromArray(t,e=0){return this.x=t[e],this.y=t[e+1],this}toArray(t=[],e=0){return t[e]=this.x,t[e+1]=this.y,t}fromBufferAttribute(t,e){return this.x=t.getX(e),this.y=t.getY(e),this}rotateAround(t,e){const n=Math.cos(e),i=Math.sin(e),r=this.x-t.x,a=this.y-t.y;return this.x=r*n-a*i+t.x,this.y=r*i+a*n+t.y,this}random(){return this.x=Math.random(),this.y=Math.random(),this}*[Symbol.iterator](){yield this.x,yield this.y}}class Kn{constructor(t=0,e=0,n=0,i=1){this.isQuaternion=!0,this._x=t,this._y=e,this._z=n,this._w=i}static slerpFlat(t,e,n,i,r,a,o){let l=n[i+0],c=n[i+1],h=n[i+2],d=n[i+3];const u=r[a+0],p=r[a+1],g=r[a+2],v=r[a+3];if(o===0){t[e+0]=l,t[e+1]=c,t[e+2]=h,t[e+3]=d;return}if(o===1){t[e+0]=u,t[e+1]=p,t[e+2]=g,t[e+3]=v;return}if(d!==v||l!==u||c!==p||h!==g){let m=1-o;const f=l*u+c*p+h*g+d*v,E=f>=0?1:-1,b=1-f*f;if(b>Number.EPSILON){const w=Math.sqrt(b),A=Math.atan2(w,f*E);m=Math.sin(m*A)/w,o=Math.sin(o*A)/w}const S=o*E;if(l=l*m+u*S,c=c*m+p*S,h=h*m+g*S,d=d*m+v*S,m===1-o){const w=1/Math.sqrt(l*l+c*c+h*h+d*d);l*=w,c*=w,h*=w,d*=w}}t[e]=l,t[e+1]=c,t[e+2]=h,t[e+3]=d}static multiplyQuaternionsFlat(t,e,n,i,r,a){const o=n[i],l=n[i+1],c=n[i+2],h=n[i+3],d=r[a],u=r[a+1],p=r[a+2],g=r[a+3];return t[e]=o*g+h*d+l*p-c*u,t[e+1]=l*g+h*u+c*d-o*p,t[e+2]=c*g+h*p+o*u-l*d,t[e+3]=h*g-o*d-l*u-c*p,t}get x(){return this._x}set x(t){this._x=t,this._onChangeCallback()}get y(){return this._y}set y(t){this._y=t,this._onChangeCallback()}get z(){return this._z}set z(t){this._z=t,this._onChangeCallback()}get w(){return this._w}set w(t){this._w=t,this._onChangeCallback()}set(t,e,n,i){return this._x=t,this._y=e,this._z=n,this._w=i,this._onChangeCallback(),this}clone(){return new this.constructor(this._x,this._y,this._z,this._w)}copy(t){return this._x=t.x,this._y=t.y,this._z=t.z,this._w=t.w,this._onChangeCallback(),this}setFromEuler(t,e=!0){const n=t._x,i=t._y,r=t._z,a=t._order,o=Math.cos,l=Math.sin,c=o(n/2),h=o(i/2),d=o(r/2),u=l(n/2),p=l(i/2),g=l(r/2);switch(a){case"XYZ":this._x=u*h*d+c*p*g,this._y=c*p*d-u*h*g,this._z=c*h*g+u*p*d,this._w=c*h*d-u*p*g;break;case"YXZ":this._x=u*h*d+c*p*g,this._y=c*p*d-u*h*g,this._z=c*h*g-u*p*d,this._w=c*h*d+u*p*g;break;case"ZXY":this._x=u*h*d-c*p*g,this._y=c*p*d+u*h*g,this._z=c*h*g+u*p*d,this._w=c*h*d-u*p*g;break;case"ZYX":this._x=u*h*d-c*p*g,this._y=c*p*d+u*h*g,this._z=c*h*g-u*p*d,this._w=c*h*d+u*p*g;break;case"YZX":this._x=u*h*d+c*p*g,this._y=c*p*d+u*h*g,this._z=c*h*g-u*p*d,this._w=c*h*d-u*p*g;break;case"XZY":this._x=u*h*d-c*p*g,this._y=c*p*d-u*h*g,this._z=c*h*g+u*p*d,this._w=c*h*d+u*p*g;break;default:console.warn("THREE.Quaternion: .setFromEuler() encountered an unknown order: "+a)}return e===!0&&this._onChangeCallback(),this}setFromAxisAngle(t,e){const n=e/2,i=Math.sin(n);return this._x=t.x*i,this._y=t.y*i,this._z=t.z*i,this._w=Math.cos(n),this._onChangeCallback(),this}setFromRotationMatrix(t){const e=t.elements,n=e[0],i=e[4],r=e[8],a=e[1],o=e[5],l=e[9],c=e[2],h=e[6],d=e[10],u=n+o+d;if(u>0){const p=.5/Math.sqrt(u+1);this._w=.25/p,this._x=(h-l)*p,this._y=(r-c)*p,this._z=(a-i)*p}else if(n>o&&n>d){const p=2*Math.sqrt(1+n-o-d);this._w=(h-l)/p,this._x=.25*p,this._y=(i+a)/p,this._z=(r+c)/p}else if(o>d){const p=2*Math.sqrt(1+o-n-d);this._w=(r-c)/p,this._x=(i+a)/p,this._y=.25*p,this._z=(l+h)/p}else{const p=2*Math.sqrt(1+d-n-o);this._w=(a-i)/p,this._x=(r+c)/p,this._y=(l+h)/p,this._z=.25*p}return this._onChangeCallback(),this}setFromUnitVectors(t,e){let n=t.dot(e)+1;return n<1e-8?(n=0,Math.abs(t.x)>Math.abs(t.z)?(this._x=-t.y,this._y=t.x,this._z=0,this._w=n):(this._x=0,this._y=-t.z,this._z=t.y,this._w=n)):(this._x=t.y*e.z-t.z*e.y,this._y=t.z*e.x-t.x*e.z,this._z=t.x*e.y-t.y*e.x,this._w=n),this.normalize()}angleTo(t){return 2*Math.acos(Math.abs(zt(this.dot(t),-1,1)))}rotateTowards(t,e){const n=this.angleTo(t);if(n===0)return this;const i=Math.min(1,e/n);return this.slerp(t,i),this}identity(){return this.set(0,0,0,1)}invert(){return this.conjugate()}conjugate(){return this._x*=-1,this._y*=-1,this._z*=-1,this._onChangeCallback(),this}dot(t){return this._x*t._x+this._y*t._y+this._z*t._z+this._w*t._w}lengthSq(){return this._x*this._x+this._y*this._y+this._z*this._z+this._w*this._w}length(){return Math.sqrt(this._x*this._x+this._y*this._y+this._z*this._z+this._w*this._w)}normalize(){let t=this.length();return t===0?(this._x=0,this._y=0,this._z=0,this._w=1):(t=1/t,this._x=this._x*t,this._y=this._y*t,this._z=this._z*t,this._w=this._w*t),this._onChangeCallback(),this}multiply(t){return this.multiplyQuaternions(this,t)}premultiply(t){return this.multiplyQuaternions(t,this)}multiplyQuaternions(t,e){const n=t._x,i=t._y,r=t._z,a=t._w,o=e._x,l=e._y,c=e._z,h=e._w;return this._x=n*h+a*o+i*c-r*l,this._y=i*h+a*l+r*o-n*c,this._z=r*h+a*c+n*l-i*o,this._w=a*h-n*o-i*l-r*c,this._onChangeCallback(),this}slerp(t,e){if(e===0)return this;if(e===1)return this.copy(t);const n=this._x,i=this._y,r=this._z,a=this._w;let o=a*t._w+n*t._x+i*t._y+r*t._z;if(o<0?(this._w=-t._w,this._x=-t._x,this._y=-t._y,this._z=-t._z,o=-o):this.copy(t),o>=1)return this._w=a,this._x=n,this._y=i,this._z=r,this;const l=1-o*o;if(l<=Number.EPSILON){const p=1-e;return this._w=p*a+e*this._w,this._x=p*n+e*this._x,this._y=p*i+e*this._y,this._z=p*r+e*this._z,this.normalize(),this}const c=Math.sqrt(l),h=Math.atan2(c,o),d=Math.sin((1-e)*h)/c,u=Math.sin(e*h)/c;return this._w=a*d+this._w*u,this._x=n*d+this._x*u,this._y=i*d+this._y*u,this._z=r*d+this._z*u,this._onChangeCallback(),this}slerpQuaternions(t,e,n){return this.copy(t).slerp(e,n)}random(){const t=2*Math.PI*Math.random(),e=2*Math.PI*Math.random(),n=Math.random(),i=Math.sqrt(1-n),r=Math.sqrt(n);return this.set(i*Math.sin(t),i*Math.cos(t),r*Math.sin(e),r*Math.cos(e))}equals(t){return t._x===this._x&&t._y===this._y&&t._z===this._z&&t._w===this._w}fromArray(t,e=0){return this._x=t[e],this._y=t[e+1],this._z=t[e+2],this._w=t[e+3],this._onChangeCallback(),this}toArray(t=[],e=0){return t[e]=this._x,t[e+1]=this._y,t[e+2]=this._z,t[e+3]=this._w,t}fromBufferAttribute(t,e){return this._x=t.getX(e),this._y=t.getY(e),this._z=t.getZ(e),this._w=t.getW(e),this._onChangeCallback(),this}toJSON(){return this.toArray()}_onChange(t){return this._onChangeCallback=t,this}_onChangeCallback(){}*[Symbol.iterator](){yield this._x,yield this._y,yield this._z,yield this._w}}class P{constructor(t=0,e=0,n=0){P.prototype.isVector3=!0,this.x=t,this.y=e,this.z=n}set(t,e,n){return n===void 0&&(n=this.z),this.x=t,this.y=e,this.z=n,this}setScalar(t){return this.x=t,this.y=t,this.z=t,this}setX(t){return this.x=t,this}setY(t){return this.y=t,this}setZ(t){return this.z=t,this}setComponent(t,e){switch(t){case 0:this.x=e;break;case 1:this.y=e;break;case 2:this.z=e;break;default:throw new Error("index is out of range: "+t)}return this}getComponent(t){switch(t){case 0:return this.x;case 1:return this.y;case 2:return this.z;default:throw new Error("index is out of range: "+t)}}clone(){return new this.constructor(this.x,this.y,this.z)}copy(t){return this.x=t.x,this.y=t.y,this.z=t.z,this}add(t){return this.x+=t.x,this.y+=t.y,this.z+=t.z,this}addScalar(t){return this.x+=t,this.y+=t,this.z+=t,this}addVectors(t,e){return this.x=t.x+e.x,this.y=t.y+e.y,this.z=t.z+e.z,this}addScaledVector(t,e){return this.x+=t.x*e,this.y+=t.y*e,this.z+=t.z*e,this}sub(t){return this.x-=t.x,this.y-=t.y,this.z-=t.z,this}subScalar(t){return this.x-=t,this.y-=t,this.z-=t,this}subVectors(t,e){return this.x=t.x-e.x,this.y=t.y-e.y,this.z=t.z-e.z,this}multiply(t){return this.x*=t.x,this.y*=t.y,this.z*=t.z,this}multiplyScalar(t){return this.x*=t,this.y*=t,this.z*=t,this}multiplyVectors(t,e){return this.x=t.x*e.x,this.y=t.y*e.y,this.z=t.z*e.z,this}applyEuler(t){return this.applyQuaternion(io.setFromEuler(t))}applyAxisAngle(t,e){return this.applyQuaternion(io.setFromAxisAngle(t,e))}applyMatrix3(t){const e=this.x,n=this.y,i=this.z,r=t.elements;return this.x=r[0]*e+r[3]*n+r[6]*i,this.y=r[1]*e+r[4]*n+r[7]*i,this.z=r[2]*e+r[5]*n+r[8]*i,this}applyNormalMatrix(t){return this.applyMatrix3(t).normalize()}applyMatrix4(t){const e=this.x,n=this.y,i=this.z,r=t.elements,a=1/(r[3]*e+r[7]*n+r[11]*i+r[15]);return this.x=(r[0]*e+r[4]*n+r[8]*i+r[12])*a,this.y=(r[1]*e+r[5]*n+r[9]*i+r[13])*a,this.z=(r[2]*e+r[6]*n+r[10]*i+r[14])*a,this}applyQuaternion(t){const e=this.x,n=this.y,i=this.z,r=t.x,a=t.y,o=t.z,l=t.w,c=2*(a*i-o*n),h=2*(o*e-r*i),d=2*(r*n-a*e);return this.x=e+l*c+a*d-o*h,this.y=n+l*h+o*c-r*d,this.z=i+l*d+r*h-a*c,this}project(t){return this.applyMatrix4(t.matrixWorldInverse).applyMatrix4(t.projectionMatrix)}unproject(t){return this.applyMatrix4(t.projectionMatrixInverse).applyMatrix4(t.matrixWorld)}transformDirection(t){const e=this.x,n=this.y,i=this.z,r=t.elements;return this.x=r[0]*e+r[4]*n+r[8]*i,this.y=r[1]*e+r[5]*n+r[9]*i,this.z=r[2]*e+r[6]*n+r[10]*i,this.normalize()}divide(t){return this.x/=t.x,this.y/=t.y,this.z/=t.z,this}divideScalar(t){return this.multiplyScalar(1/t)}min(t){return this.x=Math.min(this.x,t.x),this.y=Math.min(this.y,t.y),this.z=Math.min(this.z,t.z),this}max(t){return this.x=Math.max(this.x,t.x),this.y=Math.max(this.y,t.y),this.z=Math.max(this.z,t.z),this}clamp(t,e){return this.x=zt(this.x,t.x,e.x),this.y=zt(this.y,t.y,e.y),this.z=zt(this.z,t.z,e.z),this}clampScalar(t,e){return this.x=zt(this.x,t,e),this.y=zt(this.y,t,e),this.z=zt(this.z,t,e),this}clampLength(t,e){const n=this.length();return this.divideScalar(n||1).multiplyScalar(zt(n,t,e))}floor(){return this.x=Math.floor(this.x),this.y=Math.floor(this.y),this.z=Math.floor(this.z),this}ceil(){return this.x=Math.ceil(this.x),this.y=Math.ceil(this.y),this.z=Math.ceil(this.z),this}round(){return this.x=Math.round(this.x),this.y=Math.round(this.y),this.z=Math.round(this.z),this}roundToZero(){return this.x=Math.trunc(this.x),this.y=Math.trunc(this.y),this.z=Math.trunc(this.z),this}negate(){return this.x=-this.x,this.y=-this.y,this.z=-this.z,this}dot(t){return this.x*t.x+this.y*t.y+this.z*t.z}lengthSq(){return this.x*this.x+this.y*this.y+this.z*this.z}length(){return Math.sqrt(this.x*this.x+this.y*this.y+this.z*this.z)}manhattanLength(){return Math.abs(this.x)+Math.abs(this.y)+Math.abs(this.z)}normalize(){return this.divideScalar(this.length()||1)}setLength(t){return this.normalize().multiplyScalar(t)}lerp(t,e){return this.x+=(t.x-this.x)*e,this.y+=(t.y-this.y)*e,this.z+=(t.z-this.z)*e,this}lerpVectors(t,e,n){return this.x=t.x+(e.x-t.x)*n,this.y=t.y+(e.y-t.y)*n,this.z=t.z+(e.z-t.z)*n,this}cross(t){return this.crossVectors(this,t)}crossVectors(t,e){const n=t.x,i=t.y,r=t.z,a=e.x,o=e.y,l=e.z;return this.x=i*l-r*o,this.y=r*a-n*l,this.z=n*o-i*a,this}projectOnVector(t){const e=t.lengthSq();if(e===0)return this.set(0,0,0);const n=t.dot(this)/e;return this.copy(t).multiplyScalar(n)}projectOnPlane(t){return js.copy(this).projectOnVector(t),this.sub(js)}reflect(t){return this.sub(js.copy(t).multiplyScalar(2*this.dot(t)))}angleTo(t){const e=Math.sqrt(this.lengthSq()*t.lengthSq());if(e===0)return Math.PI/2;const n=this.dot(t)/e;return Math.acos(zt(n,-1,1))}distanceTo(t){return Math.sqrt(this.distanceToSquared(t))}distanceToSquared(t){const e=this.x-t.x,n=this.y-t.y,i=this.z-t.z;return e*e+n*n+i*i}manhattanDistanceTo(t){return Math.abs(this.x-t.x)+Math.abs(this.y-t.y)+Math.abs(this.z-t.z)}setFromSpherical(t){return this.setFromSphericalCoords(t.radius,t.phi,t.theta)}setFromSphericalCoords(t,e,n){const i=Math.sin(e)*t;return this.x=i*Math.sin(n),this.y=Math.cos(e)*t,this.z=i*Math.cos(n),this}setFromCylindrical(t){return this.setFromCylindricalCoords(t.radius,t.theta,t.y)}setFromCylindricalCoords(t,e,n){return this.x=t*Math.sin(e),this.y=n,this.z=t*Math.cos(e),this}setFromMatrixPosition(t){const e=t.elements;return this.x=e[12],this.y=e[13],this.z=e[14],this}setFromMatrixScale(t){const e=this.setFromMatrixColumn(t,0).length(),n=this.setFromMatrixColumn(t,1).length(),i=this.setFromMatrixColumn(t,2).length();return this.x=e,this.y=n,this.z=i,this}setFromMatrixColumn(t,e){return this.fromArray(t.elements,e*4)}setFromMatrix3Column(t,e){return this.fromArray(t.elements,e*3)}setFromEuler(t){return this.x=t._x,this.y=t._y,this.z=t._z,this}setFromColor(t){return this.x=t.r,this.y=t.g,this.z=t.b,this}equals(t){return t.x===this.x&&t.y===this.y&&t.z===this.z}fromArray(t,e=0){return this.x=t[e],this.y=t[e+1],this.z=t[e+2],this}toArray(t=[],e=0){return t[e]=this.x,t[e+1]=this.y,t[e+2]=this.z,t}fromBufferAttribute(t,e){return this.x=t.getX(e),this.y=t.getY(e),this.z=t.getZ(e),this}random(){return this.x=Math.random(),this.y=Math.random(),this.z=Math.random(),this}randomDirection(){const t=Math.random()*Math.PI*2,e=Math.random()*2-1,n=Math.sqrt(1-e*e);return this.x=n*Math.cos(t),this.y=e,this.z=n*Math.sin(t),this}*[Symbol.iterator](){yield this.x,yield this.y,yield this.z}}const js=new P,io=new Kn;class Nt{constructor(t,e,n,i,r,a,o,l,c){Nt.prototype.isMatrix3=!0,this.elements=[1,0,0,0,1,0,0,0,1],t!==void 0&&this.set(t,e,n,i,r,a,o,l,c)}set(t,e,n,i,r,a,o,l,c){const h=this.elements;return h[0]=t,h[1]=i,h[2]=o,h[3]=e,h[4]=r,h[5]=l,h[6]=n,h[7]=a,h[8]=c,this}identity(){return this.set(1,0,0,0,1,0,0,0,1),this}copy(t){const e=this.elements,n=t.elements;return e[0]=n[0],e[1]=n[1],e[2]=n[2],e[3]=n[3],e[4]=n[4],e[5]=n[5],e[6]=n[6],e[7]=n[7],e[8]=n[8],this}extractBasis(t,e,n){return t.setFromMatrix3Column(this,0),e.setFromMatrix3Column(this,1),n.setFromMatrix3Column(this,2),this}setFromMatrix4(t){const e=t.elements;return this.set(e[0],e[4],e[8],e[1],e[5],e[9],e[2],e[6],e[10]),this}multiply(t){return this.multiplyMatrices(this,t)}premultiply(t){return this.multiplyMatrices(t,this)}multiplyMatrices(t,e){const n=t.elements,i=e.elements,r=this.elements,a=n[0],o=n[3],l=n[6],c=n[1],h=n[4],d=n[7],u=n[2],p=n[5],g=n[8],v=i[0],m=i[3],f=i[6],E=i[1],b=i[4],S=i[7],w=i[2],A=i[5],C=i[8];return r[0]=a*v+o*E+l*w,r[3]=a*m+o*b+l*A,r[6]=a*f+o*S+l*C,r[1]=c*v+h*E+d*w,r[4]=c*m+h*b+d*A,r[7]=c*f+h*S+d*C,r[2]=u*v+p*E+g*w,r[5]=u*m+p*b+g*A,r[8]=u*f+p*S+g*C,this}multiplyScalar(t){const e=this.elements;return e[0]*=t,e[3]*=t,e[6]*=t,e[1]*=t,e[4]*=t,e[7]*=t,e[2]*=t,e[5]*=t,e[8]*=t,this}determinant(){const t=this.elements,e=t[0],n=t[1],i=t[2],r=t[3],a=t[4],o=t[5],l=t[6],c=t[7],h=t[8];return e*a*h-e*o*c-n*r*h+n*o*l+i*r*c-i*a*l}invert(){const t=this.elements,e=t[0],n=t[1],i=t[2],r=t[3],a=t[4],o=t[5],l=t[6],c=t[7],h=t[8],d=h*a-o*c,u=o*l-h*r,p=c*r-a*l,g=e*d+n*u+i*p;if(g===0)return this.set(0,0,0,0,0,0,0,0,0);const v=1/g;return t[0]=d*v,t[1]=(i*c-h*n)*v,t[2]=(o*n-i*a)*v,t[3]=u*v,t[4]=(h*e-i*l)*v,t[5]=(i*r-o*e)*v,t[6]=p*v,t[7]=(n*l-c*e)*v,t[8]=(a*e-n*r)*v,this}transpose(){let t;const e=this.elements;return t=e[1],e[1]=e[3],e[3]=t,t=e[2],e[2]=e[6],e[6]=t,t=e[5],e[5]=e[7],e[7]=t,this}getNormalMatrix(t){return this.setFromMatrix4(t).invert().transpose()}transposeIntoArray(t){const e=this.elements;return t[0]=e[0],t[1]=e[3],t[2]=e[6],t[3]=e[1],t[4]=e[4],t[5]=e[7],t[6]=e[2],t[7]=e[5],t[8]=e[8],this}setUvTransform(t,e,n,i,r,a,o){const l=Math.cos(r),c=Math.sin(r);return this.set(n*l,n*c,-n*(l*a+c*o)+a+t,-i*c,i*l,-i*(-c*a+l*o)+o+e,0,0,1),this}scale(t,e){return this.premultiply(Ks.makeScale(t,e)),this}rotate(t){return this.premultiply(Ks.makeRotation(-t)),this}translate(t,e){return this.premultiply(Ks.makeTranslation(t,e)),this}makeTranslation(t,e){return t.isVector2?this.set(1,0,t.x,0,1,t.y,0,0,1):this.set(1,0,t,0,1,e,0,0,1),this}makeRotation(t){const e=Math.cos(t),n=Math.sin(t);return this.set(e,-n,0,n,e,0,0,0,1),this}makeScale(t,e){return this.set(t,0,0,0,e,0,0,0,1),this}equals(t){const e=this.elements,n=t.elements;for(let i=0;i<9;i++)if(e[i]!==n[i])return!1;return!0}fromArray(t,e=0){for(let n=0;n<9;n++)this.elements[n]=t[n+e];return this}toArray(t=[],e=0){const n=this.elements;return t[e]=n[0],t[e+1]=n[1],t[e+2]=n[2],t[e+3]=n[3],t[e+4]=n[4],t[e+5]=n[5],t[e+6]=n[6],t[e+7]=n[7],t[e+8]=n[8],t}clone(){return new this.constructor().fromArray(this.elements)}}const Ks=new Nt;function Il(s){for(let t=s.length-1;t>=0;--t)if(s[t]>=65535)return!0;return!1}function Bs(s){return document.createElementNS("http://www.w3.org/1999/xhtml",s)}function eh(){const s=Bs("canvas");return s.style.display="block",s}const so={};function Ki(s){s in so||(so[s]=!0,console.warn(s))}function nh(s,t,e){return new Promise(function(n,i){function r(){switch(s.clientWaitSync(t,s.SYNC_FLUSH_COMMANDS_BIT,0)){case s.WAIT_FAILED:i();break;case s.TIMEOUT_EXPIRED:setTimeout(r,e);break;default:n()}}setTimeout(r,e)})}const ro=new Nt().set(.4123908,.3575843,.1804808,.212639,.7151687,.0721923,.0193308,.1191948,.9505322),ao=new Nt().set(3.2409699,-1.5373832,-.4986108,-.9692436,1.8759675,.0415551,.0556301,-.203977,1.0569715);function ih(){const s={enabled:!0,workingColorSpace:Ei,spaces:{},convert:function(i,r,a){return this.enabled===!1||r===a||!r||!a||(this.spaces[r].transfer===jt&&(i.r=Mn(i.r),i.g=Mn(i.g),i.b=Mn(i.b)),this.spaces[r].primaries!==this.spaces[a].primaries&&(i.applyMatrix3(this.spaces[r].toXYZ),i.applyMatrix3(this.spaces[a].fromXYZ)),this.spaces[a].transfer===jt&&(i.r=vi(i.r),i.g=vi(i.g),i.b=vi(i.b))),i},workingToColorSpace:function(i,r){return this.convert(i,this.workingColorSpace,r)},colorSpaceToWorking:function(i,r){return this.convert(i,r,this.workingColorSpace)},getPrimaries:function(i){return this.spaces[i].primaries},getTransfer:function(i){return i===Rn?Fs:this.spaces[i].transfer},getToneMappingMode:function(i){return this.spaces[i].outputColorSpaceConfig.toneMappingMode||"standard"},getLuminanceCoefficients:function(i,r=this.workingColorSpace){return i.fromArray(this.spaces[r].luminanceCoefficients)},define:function(i){Object.assign(this.spaces,i)},_getMatrix:function(i,r,a){return i.copy(this.spaces[r].toXYZ).multiply(this.spaces[a].fromXYZ)},_getDrawingBufferColorSpace:function(i){return this.spaces[i].outputColorSpaceConfig.drawingBufferColorSpace},_getUnpackColorSpace:function(i=this.workingColorSpace){return this.spaces[i].workingColorSpaceConfig.unpackColorSpace},fromWorkingColorSpace:function(i,r){return Ki("THREE.ColorManagement: .fromWorkingColorSpace() has been renamed to .workingToColorSpace()."),s.workingToColorSpace(i,r)},toWorkingColorSpace:function(i,r){return Ki("THREE.ColorManagement: .toWorkingColorSpace() has been renamed to .colorSpaceToWorking()."),s.colorSpaceToWorking(i,r)}},t=[.64,.33,.3,.6,.15,.06],e=[.2126,.7152,.0722],n=[.3127,.329];return s.define({[Ei]:{primaries:t,whitePoint:n,transfer:Fs,toXYZ:ro,fromXYZ:ao,luminanceCoefficients:e,workingColorSpaceConfig:{unpackColorSpace:Be},outputColorSpaceConfig:{drawingBufferColorSpace:Be}},[Be]:{primaries:t,whitePoint:n,transfer:jt,toXYZ:ro,fromXYZ:ao,luminanceCoefficients:e,outputColorSpaceConfig:{drawingBufferColorSpace:Be}}}),s}const Gt=ih();function Mn(s){return s<.04045?s*.0773993808:Math.pow(s*.9478672986+.0521327014,2.4)}function vi(s){return s<.0031308?s*12.92:1.055*Math.pow(s,.41666)-.055}let ni;class sh{static getDataURL(t,e="image/png"){if(/^data:/i.test(t.src)||typeof HTMLCanvasElement>"u")return t.src;let n;if(t instanceof HTMLCanvasElement)n=t;else{ni===void 0&&(ni=Bs("canvas")),ni.width=t.width,ni.height=t.height;const i=ni.getContext("2d");t instanceof ImageData?i.putImageData(t,0,0):i.drawImage(t,0,0,t.width,t.height),n=ni}return n.toDataURL(e)}static sRGBToLinear(t){if(typeof HTMLImageElement<"u"&&t instanceof HTMLImageElement||typeof HTMLCanvasElement<"u"&&t instanceof HTMLCanvasElement||typeof ImageBitmap<"u"&&t instanceof ImageBitmap){const e=Bs("canvas");e.width=t.width,e.height=t.height;const n=e.getContext("2d");n.drawImage(t,0,0,t.width,t.height);const i=n.getImageData(0,0,t.width,t.height),r=i.data;for(let a=0;a<r.length;a++)r[a]=Mn(r[a]/255)*255;return n.putImageData(i,0,0),e}else if(t.data){const e=t.data.slice(0);for(let n=0;n<e.length;n++)e instanceof Uint8Array||e instanceof Uint8ClampedArray?e[n]=Math.floor(Mn(e[n]/255)*255):e[n]=Mn(e[n]);return{data:e,width:t.width,height:t.height}}else return console.warn("THREE.ImageUtils.sRGBToLinear(): Unsupported image type. No color space conversion applied."),t}}let rh=0;class La{constructor(t=null){this.isSource=!0,Object.defineProperty(this,"id",{value:rh++}),this.uuid=wi(),this.data=t,this.dataReady=!0,this.version=0}getSize(t){const e=this.data;return typeof HTMLVideoElement<"u"&&e instanceof HTMLVideoElement?t.set(e.videoWidth,e.videoHeight,0):e instanceof VideoFrame?t.set(e.displayHeight,e.displayWidth,0):e!==null?t.set(e.width,e.height,e.depth||0):t.set(0,0,0),t}set needsUpdate(t){t===!0&&this.version++}toJSON(t){const e=t===void 0||typeof t=="string";if(!e&&t.images[this.uuid]!==void 0)return t.images[this.uuid];const n={uuid:this.uuid,url:""},i=this.data;if(i!==null){let r;if(Array.isArray(i)){r=[];for(let a=0,o=i.length;a<o;a++)i[a].isDataTexture?r.push(Zs(i[a].image)):r.push(Zs(i[a]))}else r=Zs(i);n.url=r}return e||(t.images[this.uuid]=n),n}}function Zs(s){return typeof HTMLImageElement<"u"&&s instanceof HTMLImageElement||typeof HTMLCanvasElement<"u"&&s instanceof HTMLCanvasElement||typeof ImageBitmap<"u"&&s instanceof ImageBitmap?sh.getDataURL(s):s.data?{data:Array.from(s.data),width:s.width,height:s.height,type:s.data.constructor.name}:(console.warn("THREE.Texture: Unable to serialize Texture."),{})}let ah=0;const $s=new P;class Re extends Zn{constructor(t=Re.DEFAULT_IMAGE,e=Re.DEFAULT_MAPPING,n=Wn,i=Wn,r=rn,a=Xn,o=Ze,l=ln,c=Re.DEFAULT_ANISOTROPY,h=Rn){super(),this.isTexture=!0,Object.defineProperty(this,"id",{value:ah++}),this.uuid=wi(),this.name="",this.source=new La(t),this.mipmaps=[],this.mapping=e,this.channel=0,this.wrapS=n,this.wrapT=i,this.magFilter=r,this.minFilter=a,this.anisotropy=c,this.format=o,this.internalFormat=null,this.type=l,this.offset=new dt(0,0),this.repeat=new dt(1,1),this.center=new dt(0,0),this.rotation=0,this.matrixAutoUpdate=!0,this.matrix=new Nt,this.generateMipmaps=!0,this.premultiplyAlpha=!1,this.flipY=!0,this.unpackAlignment=4,this.colorSpace=h,this.userData={},this.updateRanges=[],this.version=0,this.onUpdate=null,this.renderTarget=null,this.isRenderTargetTexture=!1,this.isArrayTexture=!!(t&&t.depth&&t.depth>1),this.pmremVersion=0}get width(){return this.source.getSize($s).x}get height(){return this.source.getSize($s).y}get depth(){return this.source.getSize($s).z}get image(){return this.source.data}set image(t=null){this.source.data=t}updateMatrix(){this.matrix.setUvTransform(this.offset.x,this.offset.y,this.repeat.x,this.repeat.y,this.rotation,this.center.x,this.center.y)}addUpdateRange(t,e){this.updateRanges.push({start:t,count:e})}clearUpdateRanges(){this.updateRanges.length=0}clone(){return new this.constructor().copy(this)}copy(t){return this.name=t.name,this.source=t.source,this.mipmaps=t.mipmaps.slice(0),this.mapping=t.mapping,this.channel=t.channel,this.wrapS=t.wrapS,this.wrapT=t.wrapT,this.magFilter=t.magFilter,this.minFilter=t.minFilter,this.anisotropy=t.anisotropy,this.format=t.format,this.internalFormat=t.internalFormat,this.type=t.type,this.offset.copy(t.offset),this.repeat.copy(t.repeat),this.center.copy(t.center),this.rotation=t.rotation,this.matrixAutoUpdate=t.matrixAutoUpdate,this.matrix.copy(t.matrix),this.generateMipmaps=t.generateMipmaps,this.premultiplyAlpha=t.premultiplyAlpha,this.flipY=t.flipY,this.unpackAlignment=t.unpackAlignment,this.colorSpace=t.colorSpace,this.renderTarget=t.renderTarget,this.isRenderTargetTexture=t.isRenderTargetTexture,this.isArrayTexture=t.isArrayTexture,this.userData=JSON.parse(JSON.stringify(t.userData)),this.needsUpdate=!0,this}setValues(t){for(const e in t){const n=t[e];if(n===void 0){console.warn(`THREE.Texture.setValues(): parameter '${e}' has value of undefined.`);continue}const i=this[e];if(i===void 0){console.warn(`THREE.Texture.setValues(): property '${e}' does not exist.`);continue}i&&n&&i.isVector2&&n.isVector2||i&&n&&i.isVector3&&n.isVector3||i&&n&&i.isMatrix3&&n.isMatrix3?i.copy(n):this[e]=n}}toJSON(t){const e=t===void 0||typeof t=="string";if(!e&&t.textures[this.uuid]!==void 0)return t.textures[this.uuid];const n={metadata:{version:4.7,type:"Texture",generator:"Texture.toJSON"},uuid:this.uuid,name:this.name,image:this.source.toJSON(t).uuid,mapping:this.mapping,channel:this.channel,repeat:[this.repeat.x,this.repeat.y],offset:[this.offset.x,this.offset.y],center:[this.center.x,this.center.y],rotation:this.rotation,wrap:[this.wrapS,this.wrapT],format:this.format,internalFormat:this.internalFormat,type:this.type,colorSpace:this.colorSpace,minFilter:this.minFilter,magFilter:this.magFilter,anisotropy:this.anisotropy,flipY:this.flipY,generateMipmaps:this.generateMipmaps,premultiplyAlpha:this.premultiplyAlpha,unpackAlignment:this.unpackAlignment};return Object.keys(this.userData).length>0&&(n.userData=this.userData),e||(t.textures[this.uuid]=n),n}dispose(){this.dispatchEvent({type:"dispose"})}transformUv(t){if(this.mapping!==El)return t;if(t.applyMatrix3(this.matrix),t.x<0||t.x>1)switch(this.wrapS){case Vr:t.x=t.x-Math.floor(t.x);break;case Wn:t.x=t.x<0?0:1;break;case Gr:Math.abs(Math.floor(t.x)%2)===1?t.x=Math.ceil(t.x)-t.x:t.x=t.x-Math.floor(t.x);break}if(t.y<0||t.y>1)switch(this.wrapT){case Vr:t.y=t.y-Math.floor(t.y);break;case Wn:t.y=t.y<0?0:1;break;case Gr:Math.abs(Math.floor(t.y)%2)===1?t.y=Math.ceil(t.y)-t.y:t.y=t.y-Math.floor(t.y);break}return this.flipY&&(t.y=1-t.y),t}set needsUpdate(t){t===!0&&(this.version++,this.source.needsUpdate=!0)}set needsPMREMUpdate(t){t===!0&&this.pmremVersion++}}Re.DEFAULT_IMAGE=null;Re.DEFAULT_MAPPING=El;Re.DEFAULT_ANISOTROPY=1;class Jt{constructor(t=0,e=0,n=0,i=1){Jt.prototype.isVector4=!0,this.x=t,this.y=e,this.z=n,this.w=i}get width(){return this.z}set width(t){this.z=t}get height(){return this.w}set height(t){this.w=t}set(t,e,n,i){return this.x=t,this.y=e,this.z=n,this.w=i,this}setScalar(t){return this.x=t,this.y=t,this.z=t,this.w=t,this}setX(t){return this.x=t,this}setY(t){return this.y=t,this}setZ(t){return this.z=t,this}setW(t){return this.w=t,this}setComponent(t,e){switch(t){case 0:this.x=e;break;case 1:this.y=e;break;case 2:this.z=e;break;case 3:this.w=e;break;default:throw new Error("index is out of range: "+t)}return this}getComponent(t){switch(t){case 0:return this.x;case 1:return this.y;case 2:return this.z;case 3:return this.w;default:throw new Error("index is out of range: "+t)}}clone(){return new this.constructor(this.x,this.y,this.z,this.w)}copy(t){return this.x=t.x,this.y=t.y,this.z=t.z,this.w=t.w!==void 0?t.w:1,this}add(t){return this.x+=t.x,this.y+=t.y,this.z+=t.z,this.w+=t.w,this}addScalar(t){return this.x+=t,this.y+=t,this.z+=t,this.w+=t,this}addVectors(t,e){return this.x=t.x+e.x,this.y=t.y+e.y,this.z=t.z+e.z,this.w=t.w+e.w,this}addScaledVector(t,e){return this.x+=t.x*e,this.y+=t.y*e,this.z+=t.z*e,this.w+=t.w*e,this}sub(t){return this.x-=t.x,this.y-=t.y,this.z-=t.z,this.w-=t.w,this}subScalar(t){return this.x-=t,this.y-=t,this.z-=t,this.w-=t,this}subVectors(t,e){return this.x=t.x-e.x,this.y=t.y-e.y,this.z=t.z-e.z,this.w=t.w-e.w,this}multiply(t){return this.x*=t.x,this.y*=t.y,this.z*=t.z,this.w*=t.w,this}multiplyScalar(t){return this.x*=t,this.y*=t,this.z*=t,this.w*=t,this}applyMatrix4(t){const e=this.x,n=this.y,i=this.z,r=this.w,a=t.elements;return this.x=a[0]*e+a[4]*n+a[8]*i+a[12]*r,this.y=a[1]*e+a[5]*n+a[9]*i+a[13]*r,this.z=a[2]*e+a[6]*n+a[10]*i+a[14]*r,this.w=a[3]*e+a[7]*n+a[11]*i+a[15]*r,this}divide(t){return this.x/=t.x,this.y/=t.y,this.z/=t.z,this.w/=t.w,this}divideScalar(t){return this.multiplyScalar(1/t)}setAxisAngleFromQuaternion(t){this.w=2*Math.acos(t.w);const e=Math.sqrt(1-t.w*t.w);return e<1e-4?(this.x=1,this.y=0,this.z=0):(this.x=t.x/e,this.y=t.y/e,this.z=t.z/e),this}setAxisAngleFromRotationMatrix(t){let e,n,i,r;const l=t.elements,c=l[0],h=l[4],d=l[8],u=l[1],p=l[5],g=l[9],v=l[2],m=l[6],f=l[10];if(Math.abs(h-u)<.01&&Math.abs(d-v)<.01&&Math.abs(g-m)<.01){if(Math.abs(h+u)<.1&&Math.abs(d+v)<.1&&Math.abs(g+m)<.1&&Math.abs(c+p+f-3)<.1)return this.set(1,0,0,0),this;e=Math.PI;const b=(c+1)/2,S=(p+1)/2,w=(f+1)/2,A=(h+u)/4,C=(d+v)/4,D=(g+m)/4;return b>S&&b>w?b<.01?(n=0,i=.707106781,r=.707106781):(n=Math.sqrt(b),i=A/n,r=C/n):S>w?S<.01?(n=.707106781,i=0,r=.707106781):(i=Math.sqrt(S),n=A/i,r=D/i):w<.01?(n=.707106781,i=.707106781,r=0):(r=Math.sqrt(w),n=C/r,i=D/r),this.set(n,i,r,e),this}let E=Math.sqrt((m-g)*(m-g)+(d-v)*(d-v)+(u-h)*(u-h));return Math.abs(E)<.001&&(E=1),this.x=(m-g)/E,this.y=(d-v)/E,this.z=(u-h)/E,this.w=Math.acos((c+p+f-1)/2),this}setFromMatrixPosition(t){const e=t.elements;return this.x=e[12],this.y=e[13],this.z=e[14],this.w=e[15],this}min(t){return this.x=Math.min(this.x,t.x),this.y=Math.min(this.y,t.y),this.z=Math.min(this.z,t.z),this.w=Math.min(this.w,t.w),this}max(t){return this.x=Math.max(this.x,t.x),this.y=Math.max(this.y,t.y),this.z=Math.max(this.z,t.z),this.w=Math.max(this.w,t.w),this}clamp(t,e){return this.x=zt(this.x,t.x,e.x),this.y=zt(this.y,t.y,e.y),this.z=zt(this.z,t.z,e.z),this.w=zt(this.w,t.w,e.w),this}clampScalar(t,e){return this.x=zt(this.x,t,e),this.y=zt(this.y,t,e),this.z=zt(this.z,t,e),this.w=zt(this.w,t,e),this}clampLength(t,e){const n=this.length();return this.divideScalar(n||1).multiplyScalar(zt(n,t,e))}floor(){return this.x=Math.floor(this.x),this.y=Math.floor(this.y),this.z=Math.floor(this.z),this.w=Math.floor(this.w),this}ceil(){return this.x=Math.ceil(this.x),this.y=Math.ceil(this.y),this.z=Math.ceil(this.z),this.w=Math.ceil(this.w),this}round(){return this.x=Math.round(this.x),this.y=Math.round(this.y),this.z=Math.round(this.z),this.w=Math.round(this.w),this}roundToZero(){return this.x=Math.trunc(this.x),this.y=Math.trunc(this.y),this.z=Math.trunc(this.z),this.w=Math.trunc(this.w),this}negate(){return this.x=-this.x,this.y=-this.y,this.z=-this.z,this.w=-this.w,this}dot(t){return this.x*t.x+this.y*t.y+this.z*t.z+this.w*t.w}lengthSq(){return this.x*this.x+this.y*this.y+this.z*this.z+this.w*this.w}length(){return Math.sqrt(this.x*this.x+this.y*this.y+this.z*this.z+this.w*this.w)}manhattanLength(){return Math.abs(this.x)+Math.abs(this.y)+Math.abs(this.z)+Math.abs(this.w)}normalize(){return this.divideScalar(this.length()||1)}setLength(t){return this.normalize().multiplyScalar(t)}lerp(t,e){return this.x+=(t.x-this.x)*e,this.y+=(t.y-this.y)*e,this.z+=(t.z-this.z)*e,this.w+=(t.w-this.w)*e,this}lerpVectors(t,e,n){return this.x=t.x+(e.x-t.x)*n,this.y=t.y+(e.y-t.y)*n,this.z=t.z+(e.z-t.z)*n,this.w=t.w+(e.w-t.w)*n,this}equals(t){return t.x===this.x&&t.y===this.y&&t.z===this.z&&t.w===this.w}fromArray(t,e=0){return this.x=t[e],this.y=t[e+1],this.z=t[e+2],this.w=t[e+3],this}toArray(t=[],e=0){return t[e]=this.x,t[e+1]=this.y,t[e+2]=this.z,t[e+3]=this.w,t}fromBufferAttribute(t,e){return this.x=t.getX(e),this.y=t.getY(e),this.z=t.getZ(e),this.w=t.getW(e),this}random(){return this.x=Math.random(),this.y=Math.random(),this.z=Math.random(),this.w=Math.random(),this}*[Symbol.iterator](){yield this.x,yield this.y,yield this.z,yield this.w}}class oh extends Zn{constructor(t=1,e=1,n={}){super(),n=Object.assign({generateMipmaps:!1,internalFormat:null,minFilter:rn,depthBuffer:!0,stencilBuffer:!1,resolveDepthBuffer:!0,resolveStencilBuffer:!0,depthTexture:null,samples:0,count:1,depth:1,multiview:!1},n),this.isRenderTarget=!0,this.width=t,this.height=e,this.depth=n.depth,this.scissor=new Jt(0,0,t,e),this.scissorTest=!1,this.viewport=new Jt(0,0,t,e);const i={width:t,height:e,depth:n.depth},r=new Re(i);this.textures=[];const a=n.count;for(let o=0;o<a;o++)this.textures[o]=r.clone(),this.textures[o].isRenderTargetTexture=!0,this.textures[o].renderTarget=this;this._setTextureOptions(n),this.depthBuffer=n.depthBuffer,this.stencilBuffer=n.stencilBuffer,this.resolveDepthBuffer=n.resolveDepthBuffer,this.resolveStencilBuffer=n.resolveStencilBuffer,this._depthTexture=null,this.depthTexture=n.depthTexture,this.samples=n.samples,this.multiview=n.multiview}_setTextureOptions(t={}){const e={minFilter:rn,generateMipmaps:!1,flipY:!1,internalFormat:null};t.mapping!==void 0&&(e.mapping=t.mapping),t.wrapS!==void 0&&(e.wrapS=t.wrapS),t.wrapT!==void 0&&(e.wrapT=t.wrapT),t.wrapR!==void 0&&(e.wrapR=t.wrapR),t.magFilter!==void 0&&(e.magFilter=t.magFilter),t.minFilter!==void 0&&(e.minFilter=t.minFilter),t.format!==void 0&&(e.format=t.format),t.type!==void 0&&(e.type=t.type),t.anisotropy!==void 0&&(e.anisotropy=t.anisotropy),t.colorSpace!==void 0&&(e.colorSpace=t.colorSpace),t.flipY!==void 0&&(e.flipY=t.flipY),t.generateMipmaps!==void 0&&(e.generateMipmaps=t.generateMipmaps),t.internalFormat!==void 0&&(e.internalFormat=t.internalFormat);for(let n=0;n<this.textures.length;n++)this.textures[n].setValues(e)}get texture(){return this.textures[0]}set texture(t){this.textures[0]=t}set depthTexture(t){this._depthTexture!==null&&(this._depthTexture.renderTarget=null),t!==null&&(t.renderTarget=this),this._depthTexture=t}get depthTexture(){return this._depthTexture}setSize(t,e,n=1){if(this.width!==t||this.height!==e||this.depth!==n){this.width=t,this.height=e,this.depth=n;for(let i=0,r=this.textures.length;i<r;i++)this.textures[i].image.width=t,this.textures[i].image.height=e,this.textures[i].image.depth=n,this.textures[i].isArrayTexture=this.textures[i].image.depth>1;this.dispose()}this.viewport.set(0,0,t,e),this.scissor.set(0,0,t,e)}clone(){return new this.constructor().copy(this)}copy(t){this.width=t.width,this.height=t.height,this.depth=t.depth,this.scissor.copy(t.scissor),this.scissorTest=t.scissorTest,this.viewport.copy(t.viewport),this.textures.length=0;for(let e=0,n=t.textures.length;e<n;e++){this.textures[e]=t.textures[e].clone(),this.textures[e].isRenderTargetTexture=!0,this.textures[e].renderTarget=this;const i=Object.assign({},t.textures[e].image);this.textures[e].source=new La(i)}return this.depthBuffer=t.depthBuffer,this.stencilBuffer=t.stencilBuffer,this.resolveDepthBuffer=t.resolveDepthBuffer,this.resolveStencilBuffer=t.resolveStencilBuffer,t.depthTexture!==null&&(this.depthTexture=t.depthTexture.clone()),this.samples=t.samples,this}dispose(){this.dispatchEvent({type:"dispose"})}}class $e extends oh{constructor(t=1,e=1,n={}){super(t,e,n),this.isWebGLRenderTarget=!0}}class Ul extends Re{constructor(t=null,e=1,n=1,i=1){super(null),this.isDataArrayTexture=!0,this.image={data:t,width:e,height:n,depth:i},this.magFilter=ze,this.minFilter=ze,this.wrapR=Wn,this.generateMipmaps=!1,this.flipY=!1,this.unpackAlignment=1,this.layerUpdates=new Set}addLayerUpdate(t){this.layerUpdates.add(t)}clearLayerUpdates(){this.layerUpdates.clear()}}class lh extends Re{constructor(t=null,e=1,n=1,i=1){super(null),this.isData3DTexture=!0,this.image={data:t,width:e,height:n,depth:i},this.magFilter=ze,this.minFilter=ze,this.wrapR=Wn,this.generateMipmaps=!1,this.flipY=!1,this.unpackAlignment=1}}class $n{constructor(t=new P(1/0,1/0,1/0),e=new P(-1/0,-1/0,-1/0)){this.isBox3=!0,this.min=t,this.max=e}set(t,e){return this.min.copy(t),this.max.copy(e),this}setFromArray(t){this.makeEmpty();for(let e=0,n=t.length;e<n;e+=3)this.expandByPoint(qe.fromArray(t,e));return this}setFromBufferAttribute(t){this.makeEmpty();for(let e=0,n=t.count;e<n;e++)this.expandByPoint(qe.fromBufferAttribute(t,e));return this}setFromPoints(t){this.makeEmpty();for(let e=0,n=t.length;e<n;e++)this.expandByPoint(t[e]);return this}setFromCenterAndSize(t,e){const n=qe.copy(e).multiplyScalar(.5);return this.min.copy(t).sub(n),this.max.copy(t).add(n),this}setFromObject(t,e=!1){return this.makeEmpty(),this.expandByObject(t,e)}clone(){return new this.constructor().copy(this)}copy(t){return this.min.copy(t.min),this.max.copy(t.max),this}makeEmpty(){return this.min.x=this.min.y=this.min.z=1/0,this.max.x=this.max.y=this.max.z=-1/0,this}isEmpty(){return this.max.x<this.min.x||this.max.y<this.min.y||this.max.z<this.min.z}getCenter(t){return this.isEmpty()?t.set(0,0,0):t.addVectors(this.min,this.max).multiplyScalar(.5)}getSize(t){return this.isEmpty()?t.set(0,0,0):t.subVectors(this.max,this.min)}expandByPoint(t){return this.min.min(t),this.max.max(t),this}expandByVector(t){return this.min.sub(t),this.max.add(t),this}expandByScalar(t){return this.min.addScalar(-t),this.max.addScalar(t),this}expandByObject(t,e=!1){t.updateWorldMatrix(!1,!1);const n=t.geometry;if(n!==void 0){const r=n.getAttribute("position");if(e===!0&&r!==void 0&&t.isInstancedMesh!==!0)for(let a=0,o=r.count;a<o;a++)t.isMesh===!0?t.getVertexPosition(a,qe):qe.fromBufferAttribute(r,a),qe.applyMatrix4(t.matrixWorld),this.expandByPoint(qe);else t.boundingBox!==void 0?(t.boundingBox===null&&t.computeBoundingBox(),ns.copy(t.boundingBox)):(n.boundingBox===null&&n.computeBoundingBox(),ns.copy(n.boundingBox)),ns.applyMatrix4(t.matrixWorld),this.union(ns)}const i=t.children;for(let r=0,a=i.length;r<a;r++)this.expandByObject(i[r],e);return this}containsPoint(t){return t.x>=this.min.x&&t.x<=this.max.x&&t.y>=this.min.y&&t.y<=this.max.y&&t.z>=this.min.z&&t.z<=this.max.z}containsBox(t){return this.min.x<=t.min.x&&t.max.x<=this.max.x&&this.min.y<=t.min.y&&t.max.y<=this.max.y&&this.min.z<=t.min.z&&t.max.z<=this.max.z}getParameter(t,e){return e.set((t.x-this.min.x)/(this.max.x-this.min.x),(t.y-this.min.y)/(this.max.y-this.min.y),(t.z-this.min.z)/(this.max.z-this.min.z))}intersectsBox(t){return t.max.x>=this.min.x&&t.min.x<=this.max.x&&t.max.y>=this.min.y&&t.min.y<=this.max.y&&t.max.z>=this.min.z&&t.min.z<=this.max.z}intersectsSphere(t){return this.clampPoint(t.center,qe),qe.distanceToSquared(t.center)<=t.radius*t.radius}intersectsPlane(t){let e,n;return t.normal.x>0?(e=t.normal.x*this.min.x,n=t.normal.x*this.max.x):(e=t.normal.x*this.max.x,n=t.normal.x*this.min.x),t.normal.y>0?(e+=t.normal.y*this.min.y,n+=t.normal.y*this.max.y):(e+=t.normal.y*this.max.y,n+=t.normal.y*this.min.y),t.normal.z>0?(e+=t.normal.z*this.min.z,n+=t.normal.z*this.max.z):(e+=t.normal.z*this.max.z,n+=t.normal.z*this.min.z),e<=-t.constant&&n>=-t.constant}intersectsTriangle(t){if(this.isEmpty())return!1;this.getCenter(Pi),is.subVectors(this.max,Pi),ii.subVectors(t.a,Pi),si.subVectors(t.b,Pi),ri.subVectors(t.c,Pi),Sn.subVectors(si,ii),En.subVectors(ri,si),Nn.subVectors(ii,ri);let e=[0,-Sn.z,Sn.y,0,-En.z,En.y,0,-Nn.z,Nn.y,Sn.z,0,-Sn.x,En.z,0,-En.x,Nn.z,0,-Nn.x,-Sn.y,Sn.x,0,-En.y,En.x,0,-Nn.y,Nn.x,0];return!Js(e,ii,si,ri,is)||(e=[1,0,0,0,1,0,0,0,1],!Js(e,ii,si,ri,is))?!1:(ss.crossVectors(Sn,En),e=[ss.x,ss.y,ss.z],Js(e,ii,si,ri,is))}clampPoint(t,e){return e.copy(t).clamp(this.min,this.max)}distanceToPoint(t){return this.clampPoint(t,qe).distanceTo(t)}getBoundingSphere(t){return this.isEmpty()?t.makeEmpty():(this.getCenter(t.center),t.radius=this.getSize(qe).length()*.5),t}intersect(t){return this.min.max(t.min),this.max.min(t.max),this.isEmpty()&&this.makeEmpty(),this}union(t){return this.min.min(t.min),this.max.max(t.max),this}applyMatrix4(t){return this.isEmpty()?this:(un[0].set(this.min.x,this.min.y,this.min.z).applyMatrix4(t),un[1].set(this.min.x,this.min.y,this.max.z).applyMatrix4(t),un[2].set(this.min.x,this.max.y,this.min.z).applyMatrix4(t),un[3].set(this.min.x,this.max.y,this.max.z).applyMatrix4(t),un[4].set(this.max.x,this.min.y,this.min.z).applyMatrix4(t),un[5].set(this.max.x,this.min.y,this.max.z).applyMatrix4(t),un[6].set(this.max.x,this.max.y,this.min.z).applyMatrix4(t),un[7].set(this.max.x,this.max.y,this.max.z).applyMatrix4(t),this.setFromPoints(un),this)}translate(t){return this.min.add(t),this.max.add(t),this}equals(t){return t.min.equals(this.min)&&t.max.equals(this.max)}toJSON(){return{min:this.min.toArray(),max:this.max.toArray()}}fromJSON(t){return this.min.fromArray(t.min),this.max.fromArray(t.max),this}}const un=[new P,new P,new P,new P,new P,new P,new P,new P],qe=new P,ns=new $n,ii=new P,si=new P,ri=new P,Sn=new P,En=new P,Nn=new P,Pi=new P,is=new P,ss=new P,Fn=new P;function Js(s,t,e,n,i){for(let r=0,a=s.length-3;r<=a;r+=3){Fn.fromArray(s,r);const o=i.x*Math.abs(Fn.x)+i.y*Math.abs(Fn.y)+i.z*Math.abs(Fn.z),l=t.dot(Fn),c=e.dot(Fn),h=n.dot(Fn);if(Math.max(-Math.max(l,c,h),Math.min(l,c,h))>o)return!1}return!0}const ch=new $n,Li=new P,Qs=new P;class Jn{constructor(t=new P,e=-1){this.isSphere=!0,this.center=t,this.radius=e}set(t,e){return this.center.copy(t),this.radius=e,this}setFromPoints(t,e){const n=this.center;e!==void 0?n.copy(e):ch.setFromPoints(t).getCenter(n);let i=0;for(let r=0,a=t.length;r<a;r++)i=Math.max(i,n.distanceToSquared(t[r]));return this.radius=Math.sqrt(i),this}copy(t){return this.center.copy(t.center),this.radius=t.radius,this}isEmpty(){return this.radius<0}makeEmpty(){return this.center.set(0,0,0),this.radius=-1,this}containsPoint(t){return t.distanceToSquared(this.center)<=this.radius*this.radius}distanceToPoint(t){return t.distanceTo(this.center)-this.radius}intersectsSphere(t){const e=this.radius+t.radius;return t.center.distanceToSquared(this.center)<=e*e}intersectsBox(t){return t.intersectsSphere(this)}intersectsPlane(t){return Math.abs(t.distanceToPoint(this.center))<=this.radius}clampPoint(t,e){const n=this.center.distanceToSquared(t);return e.copy(t),n>this.radius*this.radius&&(e.sub(this.center).normalize(),e.multiplyScalar(this.radius).add(this.center)),e}getBoundingBox(t){return this.isEmpty()?(t.makeEmpty(),t):(t.set(this.center,this.center),t.expandByScalar(this.radius),t)}applyMatrix4(t){return this.center.applyMatrix4(t),this.radius=this.radius*t.getMaxScaleOnAxis(),this}translate(t){return this.center.add(t),this}expandByPoint(t){if(this.isEmpty())return this.center.copy(t),this.radius=0,this;Li.subVectors(t,this.center);const e=Li.lengthSq();if(e>this.radius*this.radius){const n=Math.sqrt(e),i=(n-this.radius)*.5;this.center.addScaledVector(Li,i/n),this.radius+=i}return this}union(t){return t.isEmpty()?this:this.isEmpty()?(this.copy(t),this):(this.center.equals(t.center)===!0?this.radius=Math.max(this.radius,t.radius):(Qs.subVectors(t.center,this.center).setLength(t.radius),this.expandByPoint(Li.copy(t.center).add(Qs)),this.expandByPoint(Li.copy(t.center).sub(Qs))),this)}equals(t){return t.center.equals(this.center)&&t.radius===this.radius}clone(){return new this.constructor().copy(this)}toJSON(){return{radius:this.radius,center:this.center.toArray()}}fromJSON(t){return this.radius=t.radius,this.center.fromArray(t.center),this}}const dn=new P,tr=new P,rs=new P,bn=new P,er=new P,as=new P,nr=new P;class $i{constructor(t=new P,e=new P(0,0,-1)){this.origin=t,this.direction=e}set(t,e){return this.origin.copy(t),this.direction.copy(e),this}copy(t){return this.origin.copy(t.origin),this.direction.copy(t.direction),this}at(t,e){return e.copy(this.origin).addScaledVector(this.direction,t)}lookAt(t){return this.direction.copy(t).sub(this.origin).normalize(),this}recast(t){return this.origin.copy(this.at(t,dn)),this}closestPointToPoint(t,e){e.subVectors(t,this.origin);const n=e.dot(this.direction);return n<0?e.copy(this.origin):e.copy(this.origin).addScaledVector(this.direction,n)}distanceToPoint(t){return Math.sqrt(this.distanceSqToPoint(t))}distanceSqToPoint(t){const e=dn.subVectors(t,this.origin).dot(this.direction);return e<0?this.origin.distanceToSquared(t):(dn.copy(this.origin).addScaledVector(this.direction,e),dn.distanceToSquared(t))}distanceSqToSegment(t,e,n,i){tr.copy(t).add(e).multiplyScalar(.5),rs.copy(e).sub(t).normalize(),bn.copy(this.origin).sub(tr);const r=t.distanceTo(e)*.5,a=-this.direction.dot(rs),o=bn.dot(this.direction),l=-bn.dot(rs),c=bn.lengthSq(),h=Math.abs(1-a*a);let d,u,p,g;if(h>0)if(d=a*l-o,u=a*o-l,g=r*h,d>=0)if(u>=-g)if(u<=g){const v=1/h;d*=v,u*=v,p=d*(d+a*u+2*o)+u*(a*d+u+2*l)+c}else u=r,d=Math.max(0,-(a*u+o)),p=-d*d+u*(u+2*l)+c;else u=-r,d=Math.max(0,-(a*u+o)),p=-d*d+u*(u+2*l)+c;else u<=-g?(d=Math.max(0,-(-a*r+o)),u=d>0?-r:Math.min(Math.max(-r,-l),r),p=-d*d+u*(u+2*l)+c):u<=g?(d=0,u=Math.min(Math.max(-r,-l),r),p=u*(u+2*l)+c):(d=Math.max(0,-(a*r+o)),u=d>0?r:Math.min(Math.max(-r,-l),r),p=-d*d+u*(u+2*l)+c);else u=a>0?-r:r,d=Math.max(0,-(a*u+o)),p=-d*d+u*(u+2*l)+c;return n&&n.copy(this.origin).addScaledVector(this.direction,d),i&&i.copy(tr).addScaledVector(rs,u),p}intersectSphere(t,e){dn.subVectors(t.center,this.origin);const n=dn.dot(this.direction),i=dn.dot(dn)-n*n,r=t.radius*t.radius;if(i>r)return null;const a=Math.sqrt(r-i),o=n-a,l=n+a;return l<0?null:o<0?this.at(l,e):this.at(o,e)}intersectsSphere(t){return t.radius<0?!1:this.distanceSqToPoint(t.center)<=t.radius*t.radius}distanceToPlane(t){const e=t.normal.dot(this.direction);if(e===0)return t.distanceToPoint(this.origin)===0?0:null;const n=-(this.origin.dot(t.normal)+t.constant)/e;return n>=0?n:null}intersectPlane(t,e){const n=this.distanceToPlane(t);return n===null?null:this.at(n,e)}intersectsPlane(t){const e=t.distanceToPoint(this.origin);return e===0||t.normal.dot(this.direction)*e<0}intersectBox(t,e){let n,i,r,a,o,l;const c=1/this.direction.x,h=1/this.direction.y,d=1/this.direction.z,u=this.origin;return c>=0?(n=(t.min.x-u.x)*c,i=(t.max.x-u.x)*c):(n=(t.max.x-u.x)*c,i=(t.min.x-u.x)*c),h>=0?(r=(t.min.y-u.y)*h,a=(t.max.y-u.y)*h):(r=(t.max.y-u.y)*h,a=(t.min.y-u.y)*h),n>a||r>i||((r>n||isNaN(n))&&(n=r),(a<i||isNaN(i))&&(i=a),d>=0?(o=(t.min.z-u.z)*d,l=(t.max.z-u.z)*d):(o=(t.max.z-u.z)*d,l=(t.min.z-u.z)*d),n>l||o>i)||((o>n||n!==n)&&(n=o),(l<i||i!==i)&&(i=l),i<0)?null:this.at(n>=0?n:i,e)}intersectsBox(t){return this.intersectBox(t,dn)!==null}intersectTriangle(t,e,n,i,r){er.subVectors(e,t),as.subVectors(n,t),nr.crossVectors(er,as);let a=this.direction.dot(nr),o;if(a>0){if(i)return null;o=1}else if(a<0)o=-1,a=-a;else return null;bn.subVectors(this.origin,t);const l=o*this.direction.dot(as.crossVectors(bn,as));if(l<0)return null;const c=o*this.direction.dot(er.cross(bn));if(c<0||l+c>a)return null;const h=-o*bn.dot(nr);return h<0?null:this.at(h/a,r)}applyMatrix4(t){return this.origin.applyMatrix4(t),this.direction.transformDirection(t),this}equals(t){return t.origin.equals(this.origin)&&t.direction.equals(this.direction)}clone(){return new this.constructor().copy(this)}}class Kt{constructor(t,e,n,i,r,a,o,l,c,h,d,u,p,g,v,m){Kt.prototype.isMatrix4=!0,this.elements=[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1],t!==void 0&&this.set(t,e,n,i,r,a,o,l,c,h,d,u,p,g,v,m)}set(t,e,n,i,r,a,o,l,c,h,d,u,p,g,v,m){const f=this.elements;return f[0]=t,f[4]=e,f[8]=n,f[12]=i,f[1]=r,f[5]=a,f[9]=o,f[13]=l,f[2]=c,f[6]=h,f[10]=d,f[14]=u,f[3]=p,f[7]=g,f[11]=v,f[15]=m,this}identity(){return this.set(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1),this}clone(){return new Kt().fromArray(this.elements)}copy(t){const e=this.elements,n=t.elements;return e[0]=n[0],e[1]=n[1],e[2]=n[2],e[3]=n[3],e[4]=n[4],e[5]=n[5],e[6]=n[6],e[7]=n[7],e[8]=n[8],e[9]=n[9],e[10]=n[10],e[11]=n[11],e[12]=n[12],e[13]=n[13],e[14]=n[14],e[15]=n[15],this}copyPosition(t){const e=this.elements,n=t.elements;return e[12]=n[12],e[13]=n[13],e[14]=n[14],this}setFromMatrix3(t){const e=t.elements;return this.set(e[0],e[3],e[6],0,e[1],e[4],e[7],0,e[2],e[5],e[8],0,0,0,0,1),this}extractBasis(t,e,n){return t.setFromMatrixColumn(this,0),e.setFromMatrixColumn(this,1),n.setFromMatrixColumn(this,2),this}makeBasis(t,e,n){return this.set(t.x,e.x,n.x,0,t.y,e.y,n.y,0,t.z,e.z,n.z,0,0,0,0,1),this}extractRotation(t){const e=this.elements,n=t.elements,i=1/ai.setFromMatrixColumn(t,0).length(),r=1/ai.setFromMatrixColumn(t,1).length(),a=1/ai.setFromMatrixColumn(t,2).length();return e[0]=n[0]*i,e[1]=n[1]*i,e[2]=n[2]*i,e[3]=0,e[4]=n[4]*r,e[5]=n[5]*r,e[6]=n[6]*r,e[7]=0,e[8]=n[8]*a,e[9]=n[9]*a,e[10]=n[10]*a,e[11]=0,e[12]=0,e[13]=0,e[14]=0,e[15]=1,this}makeRotationFromEuler(t){const e=this.elements,n=t.x,i=t.y,r=t.z,a=Math.cos(n),o=Math.sin(n),l=Math.cos(i),c=Math.sin(i),h=Math.cos(r),d=Math.sin(r);if(t.order==="XYZ"){const u=a*h,p=a*d,g=o*h,v=o*d;e[0]=l*h,e[4]=-l*d,e[8]=c,e[1]=p+g*c,e[5]=u-v*c,e[9]=-o*l,e[2]=v-u*c,e[6]=g+p*c,e[10]=a*l}else if(t.order==="YXZ"){const u=l*h,p=l*d,g=c*h,v=c*d;e[0]=u+v*o,e[4]=g*o-p,e[8]=a*c,e[1]=a*d,e[5]=a*h,e[9]=-o,e[2]=p*o-g,e[6]=v+u*o,e[10]=a*l}else if(t.order==="ZXY"){const u=l*h,p=l*d,g=c*h,v=c*d;e[0]=u-v*o,e[4]=-a*d,e[8]=g+p*o,e[1]=p+g*o,e[5]=a*h,e[9]=v-u*o,e[2]=-a*c,e[6]=o,e[10]=a*l}else if(t.order==="ZYX"){const u=a*h,p=a*d,g=o*h,v=o*d;e[0]=l*h,e[4]=g*c-p,e[8]=u*c+v,e[1]=l*d,e[5]=v*c+u,e[9]=p*c-g,e[2]=-c,e[6]=o*l,e[10]=a*l}else if(t.order==="YZX"){const u=a*l,p=a*c,g=o*l,v=o*c;e[0]=l*h,e[4]=v-u*d,e[8]=g*d+p,e[1]=d,e[5]=a*h,e[9]=-o*h,e[2]=-c*h,e[6]=p*d+g,e[10]=u-v*d}else if(t.order==="XZY"){const u=a*l,p=a*c,g=o*l,v=o*c;e[0]=l*h,e[4]=-d,e[8]=c*h,e[1]=u*d+v,e[5]=a*h,e[9]=p*d-g,e[2]=g*d-p,e[6]=o*h,e[10]=v*d+u}return e[3]=0,e[7]=0,e[11]=0,e[12]=0,e[13]=0,e[14]=0,e[15]=1,this}makeRotationFromQuaternion(t){return this.compose(hh,t,uh)}lookAt(t,e,n){const i=this.elements;return Fe.subVectors(t,e),Fe.lengthSq()===0&&(Fe.z=1),Fe.normalize(),Tn.crossVectors(n,Fe),Tn.lengthSq()===0&&(Math.abs(n.z)===1?Fe.x+=1e-4:Fe.z+=1e-4,Fe.normalize(),Tn.crossVectors(n,Fe)),Tn.normalize(),os.crossVectors(Fe,Tn),i[0]=Tn.x,i[4]=os.x,i[8]=Fe.x,i[1]=Tn.y,i[5]=os.y,i[9]=Fe.y,i[2]=Tn.z,i[6]=os.z,i[10]=Fe.z,this}multiply(t){return this.multiplyMatrices(this,t)}premultiply(t){return this.multiplyMatrices(t,this)}multiplyMatrices(t,e){const n=t.elements,i=e.elements,r=this.elements,a=n[0],o=n[4],l=n[8],c=n[12],h=n[1],d=n[5],u=n[9],p=n[13],g=n[2],v=n[6],m=n[10],f=n[14],E=n[3],b=n[7],S=n[11],w=n[15],A=i[0],C=i[4],D=i[8],y=i[12],x=i[1],L=i[5],N=i[9],k=i[13],X=i[2],W=i[6],q=i[10],Z=i[14],H=i[3],rt=i[7],ct=i[11],Et=i[15];return r[0]=a*A+o*x+l*X+c*H,r[4]=a*C+o*L+l*W+c*rt,r[8]=a*D+o*N+l*q+c*ct,r[12]=a*y+o*k+l*Z+c*Et,r[1]=h*A+d*x+u*X+p*H,r[5]=h*C+d*L+u*W+p*rt,r[9]=h*D+d*N+u*q+p*ct,r[13]=h*y+d*k+u*Z+p*Et,r[2]=g*A+v*x+m*X+f*H,r[6]=g*C+v*L+m*W+f*rt,r[10]=g*D+v*N+m*q+f*ct,r[14]=g*y+v*k+m*Z+f*Et,r[3]=E*A+b*x+S*X+w*H,r[7]=E*C+b*L+S*W+w*rt,r[11]=E*D+b*N+S*q+w*ct,r[15]=E*y+b*k+S*Z+w*Et,this}multiplyScalar(t){const e=this.elements;return e[0]*=t,e[4]*=t,e[8]*=t,e[12]*=t,e[1]*=t,e[5]*=t,e[9]*=t,e[13]*=t,e[2]*=t,e[6]*=t,e[10]*=t,e[14]*=t,e[3]*=t,e[7]*=t,e[11]*=t,e[15]*=t,this}determinant(){const t=this.elements,e=t[0],n=t[4],i=t[8],r=t[12],a=t[1],o=t[5],l=t[9],c=t[13],h=t[2],d=t[6],u=t[10],p=t[14],g=t[3],v=t[7],m=t[11],f=t[15];return g*(+r*l*d-i*c*d-r*o*u+n*c*u+i*o*p-n*l*p)+v*(+e*l*p-e*c*u+r*a*u-i*a*p+i*c*h-r*l*h)+m*(+e*c*d-e*o*p-r*a*d+n*a*p+r*o*h-n*c*h)+f*(-i*o*h-e*l*d+e*o*u+i*a*d-n*a*u+n*l*h)}transpose(){const t=this.elements;let e;return e=t[1],t[1]=t[4],t[4]=e,e=t[2],t[2]=t[8],t[8]=e,e=t[6],t[6]=t[9],t[9]=e,e=t[3],t[3]=t[12],t[12]=e,e=t[7],t[7]=t[13],t[13]=e,e=t[11],t[11]=t[14],t[14]=e,this}setPosition(t,e,n){const i=this.elements;return t.isVector3?(i[12]=t.x,i[13]=t.y,i[14]=t.z):(i[12]=t,i[13]=e,i[14]=n),this}invert(){const t=this.elements,e=t[0],n=t[1],i=t[2],r=t[3],a=t[4],o=t[5],l=t[6],c=t[7],h=t[8],d=t[9],u=t[10],p=t[11],g=t[12],v=t[13],m=t[14],f=t[15],E=d*m*c-v*u*c+v*l*p-o*m*p-d*l*f+o*u*f,b=g*u*c-h*m*c-g*l*p+a*m*p+h*l*f-a*u*f,S=h*v*c-g*d*c+g*o*p-a*v*p-h*o*f+a*d*f,w=g*d*l-h*v*l-g*o*u+a*v*u+h*o*m-a*d*m,A=e*E+n*b+i*S+r*w;if(A===0)return this.set(0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0);const C=1/A;return t[0]=E*C,t[1]=(v*u*r-d*m*r-v*i*p+n*m*p+d*i*f-n*u*f)*C,t[2]=(o*m*r-v*l*r+v*i*c-n*m*c-o*i*f+n*l*f)*C,t[3]=(d*l*r-o*u*r-d*i*c+n*u*c+o*i*p-n*l*p)*C,t[4]=b*C,t[5]=(h*m*r-g*u*r+g*i*p-e*m*p-h*i*f+e*u*f)*C,t[6]=(g*l*r-a*m*r-g*i*c+e*m*c+a*i*f-e*l*f)*C,t[7]=(a*u*r-h*l*r+h*i*c-e*u*c-a*i*p+e*l*p)*C,t[8]=S*C,t[9]=(g*d*r-h*v*r-g*n*p+e*v*p+h*n*f-e*d*f)*C,t[10]=(a*v*r-g*o*r+g*n*c-e*v*c-a*n*f+e*o*f)*C,t[11]=(h*o*r-a*d*r-h*n*c+e*d*c+a*n*p-e*o*p)*C,t[12]=w*C,t[13]=(h*v*i-g*d*i+g*n*u-e*v*u-h*n*m+e*d*m)*C,t[14]=(g*o*i-a*v*i-g*n*l+e*v*l+a*n*m-e*o*m)*C,t[15]=(a*d*i-h*o*i+h*n*l-e*d*l-a*n*u+e*o*u)*C,this}scale(t){const e=this.elements,n=t.x,i=t.y,r=t.z;return e[0]*=n,e[4]*=i,e[8]*=r,e[1]*=n,e[5]*=i,e[9]*=r,e[2]*=n,e[6]*=i,e[10]*=r,e[3]*=n,e[7]*=i,e[11]*=r,this}getMaxScaleOnAxis(){const t=this.elements,e=t[0]*t[0]+t[1]*t[1]+t[2]*t[2],n=t[4]*t[4]+t[5]*t[5]+t[6]*t[6],i=t[8]*t[8]+t[9]*t[9]+t[10]*t[10];return Math.sqrt(Math.max(e,n,i))}makeTranslation(t,e,n){return t.isVector3?this.set(1,0,0,t.x,0,1,0,t.y,0,0,1,t.z,0,0,0,1):this.set(1,0,0,t,0,1,0,e,0,0,1,n,0,0,0,1),this}makeRotationX(t){const e=Math.cos(t),n=Math.sin(t);return this.set(1,0,0,0,0,e,-n,0,0,n,e,0,0,0,0,1),this}makeRotationY(t){const e=Math.cos(t),n=Math.sin(t);return this.set(e,0,n,0,0,1,0,0,-n,0,e,0,0,0,0,1),this}makeRotationZ(t){const e=Math.cos(t),n=Math.sin(t);return this.set(e,-n,0,0,n,e,0,0,0,0,1,0,0,0,0,1),this}makeRotationAxis(t,e){const n=Math.cos(e),i=Math.sin(e),r=1-n,a=t.x,o=t.y,l=t.z,c=r*a,h=r*o;return this.set(c*a+n,c*o-i*l,c*l+i*o,0,c*o+i*l,h*o+n,h*l-i*a,0,c*l-i*o,h*l+i*a,r*l*l+n,0,0,0,0,1),this}makeScale(t,e,n){return this.set(t,0,0,0,0,e,0,0,0,0,n,0,0,0,0,1),this}makeShear(t,e,n,i,r,a){return this.set(1,n,r,0,t,1,a,0,e,i,1,0,0,0,0,1),this}compose(t,e,n){const i=this.elements,r=e._x,a=e._y,o=e._z,l=e._w,c=r+r,h=a+a,d=o+o,u=r*c,p=r*h,g=r*d,v=a*h,m=a*d,f=o*d,E=l*c,b=l*h,S=l*d,w=n.x,A=n.y,C=n.z;return i[0]=(1-(v+f))*w,i[1]=(p+S)*w,i[2]=(g-b)*w,i[3]=0,i[4]=(p-S)*A,i[5]=(1-(u+f))*A,i[6]=(m+E)*A,i[7]=0,i[8]=(g+b)*C,i[9]=(m-E)*C,i[10]=(1-(u+v))*C,i[11]=0,i[12]=t.x,i[13]=t.y,i[14]=t.z,i[15]=1,this}decompose(t,e,n){const i=this.elements;let r=ai.set(i[0],i[1],i[2]).length();const a=ai.set(i[4],i[5],i[6]).length(),o=ai.set(i[8],i[9],i[10]).length();this.determinant()<0&&(r=-r),t.x=i[12],t.y=i[13],t.z=i[14],Ye.copy(this);const c=1/r,h=1/a,d=1/o;return Ye.elements[0]*=c,Ye.elements[1]*=c,Ye.elements[2]*=c,Ye.elements[4]*=h,Ye.elements[5]*=h,Ye.elements[6]*=h,Ye.elements[8]*=d,Ye.elements[9]*=d,Ye.elements[10]*=d,e.setFromRotationMatrix(Ye),n.x=r,n.y=a,n.z=o,this}makePerspective(t,e,n,i,r,a,o=on,l=!1){const c=this.elements,h=2*r/(e-t),d=2*r/(n-i),u=(e+t)/(e-t),p=(n+i)/(n-i);let g,v;if(l)g=r/(a-r),v=a*r/(a-r);else if(o===on)g=-(a+r)/(a-r),v=-2*a*r/(a-r);else if(o===Os)g=-a/(a-r),v=-a*r/(a-r);else throw new Error("THREE.Matrix4.makePerspective(): Invalid coordinate system: "+o);return c[0]=h,c[4]=0,c[8]=u,c[12]=0,c[1]=0,c[5]=d,c[9]=p,c[13]=0,c[2]=0,c[6]=0,c[10]=g,c[14]=v,c[3]=0,c[7]=0,c[11]=-1,c[15]=0,this}makeOrthographic(t,e,n,i,r,a,o=on,l=!1){const c=this.elements,h=2/(e-t),d=2/(n-i),u=-(e+t)/(e-t),p=-(n+i)/(n-i);let g,v;if(l)g=1/(a-r),v=a/(a-r);else if(o===on)g=-2/(a-r),v=-(a+r)/(a-r);else if(o===Os)g=-1/(a-r),v=-r/(a-r);else throw new Error("THREE.Matrix4.makeOrthographic(): Invalid coordinate system: "+o);return c[0]=h,c[4]=0,c[8]=0,c[12]=u,c[1]=0,c[5]=d,c[9]=0,c[13]=p,c[2]=0,c[6]=0,c[10]=g,c[14]=v,c[3]=0,c[7]=0,c[11]=0,c[15]=1,this}equals(t){const e=this.elements,n=t.elements;for(let i=0;i<16;i++)if(e[i]!==n[i])return!1;return!0}fromArray(t,e=0){for(let n=0;n<16;n++)this.elements[n]=t[n+e];return this}toArray(t=[],e=0){const n=this.elements;return t[e]=n[0],t[e+1]=n[1],t[e+2]=n[2],t[e+3]=n[3],t[e+4]=n[4],t[e+5]=n[5],t[e+6]=n[6],t[e+7]=n[7],t[e+8]=n[8],t[e+9]=n[9],t[e+10]=n[10],t[e+11]=n[11],t[e+12]=n[12],t[e+13]=n[13],t[e+14]=n[14],t[e+15]=n[15],t}}const ai=new P,Ye=new Kt,hh=new P(0,0,0),uh=new P(1,1,1),Tn=new P,os=new P,Fe=new P,oo=new Kt,lo=new Kn;class cn{constructor(t=0,e=0,n=0,i=cn.DEFAULT_ORDER){this.isEuler=!0,this._x=t,this._y=e,this._z=n,this._order=i}get x(){return this._x}set x(t){this._x=t,this._onChangeCallback()}get y(){return this._y}set y(t){this._y=t,this._onChangeCallback()}get z(){return this._z}set z(t){this._z=t,this._onChangeCallback()}get order(){return this._order}set order(t){this._order=t,this._onChangeCallback()}set(t,e,n,i=this._order){return this._x=t,this._y=e,this._z=n,this._order=i,this._onChangeCallback(),this}clone(){return new this.constructor(this._x,this._y,this._z,this._order)}copy(t){return this._x=t._x,this._y=t._y,this._z=t._z,this._order=t._order,this._onChangeCallback(),this}setFromRotationMatrix(t,e=this._order,n=!0){const i=t.elements,r=i[0],a=i[4],o=i[8],l=i[1],c=i[5],h=i[9],d=i[2],u=i[6],p=i[10];switch(e){case"XYZ":this._y=Math.asin(zt(o,-1,1)),Math.abs(o)<.9999999?(this._x=Math.atan2(-h,p),this._z=Math.atan2(-a,r)):(this._x=Math.atan2(u,c),this._z=0);break;case"YXZ":this._x=Math.asin(-zt(h,-1,1)),Math.abs(h)<.9999999?(this._y=Math.atan2(o,p),this._z=Math.atan2(l,c)):(this._y=Math.atan2(-d,r),this._z=0);break;case"ZXY":this._x=Math.asin(zt(u,-1,1)),Math.abs(u)<.9999999?(this._y=Math.atan2(-d,p),this._z=Math.atan2(-a,c)):(this._y=0,this._z=Math.atan2(l,r));break;case"ZYX":this._y=Math.asin(-zt(d,-1,1)),Math.abs(d)<.9999999?(this._x=Math.atan2(u,p),this._z=Math.atan2(l,r)):(this._x=0,this._z=Math.atan2(-a,c));break;case"YZX":this._z=Math.asin(zt(l,-1,1)),Math.abs(l)<.9999999?(this._x=Math.atan2(-h,c),this._y=Math.atan2(-d,r)):(this._x=0,this._y=Math.atan2(o,p));break;case"XZY":this._z=Math.asin(-zt(a,-1,1)),Math.abs(a)<.9999999?(this._x=Math.atan2(u,c),this._y=Math.atan2(o,r)):(this._x=Math.atan2(-h,p),this._y=0);break;default:console.warn("THREE.Euler: .setFromRotationMatrix() encountered an unknown order: "+e)}return this._order=e,n===!0&&this._onChangeCallback(),this}setFromQuaternion(t,e,n){return oo.makeRotationFromQuaternion(t),this.setFromRotationMatrix(oo,e,n)}setFromVector3(t,e=this._order){return this.set(t.x,t.y,t.z,e)}reorder(t){return lo.setFromEuler(this),this.setFromQuaternion(lo,t)}equals(t){return t._x===this._x&&t._y===this._y&&t._z===this._z&&t._order===this._order}fromArray(t){return this._x=t[0],this._y=t[1],this._z=t[2],t[3]!==void 0&&(this._order=t[3]),this._onChangeCallback(),this}toArray(t=[],e=0){return t[e]=this._x,t[e+1]=this._y,t[e+2]=this._z,t[e+3]=this._order,t}_onChange(t){return this._onChangeCallback=t,this}_onChangeCallback(){}*[Symbol.iterator](){yield this._x,yield this._y,yield this._z,yield this._order}}cn.DEFAULT_ORDER="XYZ";class Da{constructor(){this.mask=1}set(t){this.mask=(1<<t|0)>>>0}enable(t){this.mask|=1<<t|0}enableAll(){this.mask=-1}toggle(t){this.mask^=1<<t|0}disable(t){this.mask&=~(1<<t|0)}disableAll(){this.mask=0}test(t){return(this.mask&t.mask)!==0}isEnabled(t){return(this.mask&(1<<t|0))!==0}}let dh=0;const co=new P,oi=new Kn,fn=new Kt,ls=new P,Di=new P,fh=new P,ph=new Kn,ho=new P(1,0,0),uo=new P(0,1,0),fo=new P(0,0,1),po={type:"added"},mh={type:"removed"},li={type:"childadded",child:null},ir={type:"childremoved",child:null};class fe extends Zn{constructor(){super(),this.isObject3D=!0,Object.defineProperty(this,"id",{value:dh++}),this.uuid=wi(),this.name="",this.type="Object3D",this.parent=null,this.children=[],this.up=fe.DEFAULT_UP.clone();const t=new P,e=new cn,n=new Kn,i=new P(1,1,1);function r(){n.setFromEuler(e,!1)}function a(){e.setFromQuaternion(n,void 0,!1)}e._onChange(r),n._onChange(a),Object.defineProperties(this,{position:{configurable:!0,enumerable:!0,value:t},rotation:{configurable:!0,enumerable:!0,value:e},quaternion:{configurable:!0,enumerable:!0,value:n},scale:{configurable:!0,enumerable:!0,value:i},modelViewMatrix:{value:new Kt},normalMatrix:{value:new Nt}}),this.matrix=new Kt,this.matrixWorld=new Kt,this.matrixAutoUpdate=fe.DEFAULT_MATRIX_AUTO_UPDATE,this.matrixWorldAutoUpdate=fe.DEFAULT_MATRIX_WORLD_AUTO_UPDATE,this.matrixWorldNeedsUpdate=!1,this.layers=new Da,this.visible=!0,this.castShadow=!1,this.receiveShadow=!1,this.frustumCulled=!0,this.renderOrder=0,this.animations=[],this.customDepthMaterial=void 0,this.customDistanceMaterial=void 0,this.userData={}}onBeforeShadow(){}onAfterShadow(){}onBeforeRender(){}onAfterRender(){}applyMatrix4(t){this.matrixAutoUpdate&&this.updateMatrix(),this.matrix.premultiply(t),this.matrix.decompose(this.position,this.quaternion,this.scale)}applyQuaternion(t){return this.quaternion.premultiply(t),this}setRotationFromAxisAngle(t,e){this.quaternion.setFromAxisAngle(t,e)}setRotationFromEuler(t){this.quaternion.setFromEuler(t,!0)}setRotationFromMatrix(t){this.quaternion.setFromRotationMatrix(t)}setRotationFromQuaternion(t){this.quaternion.copy(t)}rotateOnAxis(t,e){return oi.setFromAxisAngle(t,e),this.quaternion.multiply(oi),this}rotateOnWorldAxis(t,e){return oi.setFromAxisAngle(t,e),this.quaternion.premultiply(oi),this}rotateX(t){return this.rotateOnAxis(ho,t)}rotateY(t){return this.rotateOnAxis(uo,t)}rotateZ(t){return this.rotateOnAxis(fo,t)}translateOnAxis(t,e){return co.copy(t).applyQuaternion(this.quaternion),this.position.add(co.multiplyScalar(e)),this}translateX(t){return this.translateOnAxis(ho,t)}translateY(t){return this.translateOnAxis(uo,t)}translateZ(t){return this.translateOnAxis(fo,t)}localToWorld(t){return this.updateWorldMatrix(!0,!1),t.applyMatrix4(this.matrixWorld)}worldToLocal(t){return this.updateWorldMatrix(!0,!1),t.applyMatrix4(fn.copy(this.matrixWorld).invert())}lookAt(t,e,n){t.isVector3?ls.copy(t):ls.set(t,e,n);const i=this.parent;this.updateWorldMatrix(!0,!1),Di.setFromMatrixPosition(this.matrixWorld),this.isCamera||this.isLight?fn.lookAt(Di,ls,this.up):fn.lookAt(ls,Di,this.up),this.quaternion.setFromRotationMatrix(fn),i&&(fn.extractRotation(i.matrixWorld),oi.setFromRotationMatrix(fn),this.quaternion.premultiply(oi.invert()))}add(t){if(arguments.length>1){for(let e=0;e<arguments.length;e++)this.add(arguments[e]);return this}return t===this?(console.error("THREE.Object3D.add: object can't be added as a child of itself.",t),this):(t&&t.isObject3D?(t.removeFromParent(),t.parent=this,this.children.push(t),t.dispatchEvent(po),li.child=t,this.dispatchEvent(li),li.child=null):console.error("THREE.Object3D.add: object not an instance of THREE.Object3D.",t),this)}remove(t){if(arguments.length>1){for(let n=0;n<arguments.length;n++)this.remove(arguments[n]);return this}const e=this.children.indexOf(t);return e!==-1&&(t.parent=null,this.children.splice(e,1),t.dispatchEvent(mh),ir.child=t,this.dispatchEvent(ir),ir.child=null),this}removeFromParent(){const t=this.parent;return t!==null&&t.remove(this),this}clear(){return this.remove(...this.children)}attach(t){return this.updateWorldMatrix(!0,!1),fn.copy(this.matrixWorld).invert(),t.parent!==null&&(t.parent.updateWorldMatrix(!0,!1),fn.multiply(t.parent.matrixWorld)),t.applyMatrix4(fn),t.removeFromParent(),t.parent=this,this.children.push(t),t.updateWorldMatrix(!1,!0),t.dispatchEvent(po),li.child=t,this.dispatchEvent(li),li.child=null,this}getObjectById(t){return this.getObjectByProperty("id",t)}getObjectByName(t){return this.getObjectByProperty("name",t)}getObjectByProperty(t,e){if(this[t]===e)return this;for(let n=0,i=this.children.length;n<i;n++){const a=this.children[n].getObjectByProperty(t,e);if(a!==void 0)return a}}getObjectsByProperty(t,e,n=[]){this[t]===e&&n.push(this);const i=this.children;for(let r=0,a=i.length;r<a;r++)i[r].getObjectsByProperty(t,e,n);return n}getWorldPosition(t){return this.updateWorldMatrix(!0,!1),t.setFromMatrixPosition(this.matrixWorld)}getWorldQuaternion(t){return this.updateWorldMatrix(!0,!1),this.matrixWorld.decompose(Di,t,fh),t}getWorldScale(t){return this.updateWorldMatrix(!0,!1),this.matrixWorld.decompose(Di,ph,t),t}getWorldDirection(t){this.updateWorldMatrix(!0,!1);const e=this.matrixWorld.elements;return t.set(e[8],e[9],e[10]).normalize()}raycast(){}traverse(t){t(this);const e=this.children;for(let n=0,i=e.length;n<i;n++)e[n].traverse(t)}traverseVisible(t){if(this.visible===!1)return;t(this);const e=this.children;for(let n=0,i=e.length;n<i;n++)e[n].traverseVisible(t)}traverseAncestors(t){const e=this.parent;e!==null&&(t(e),e.traverseAncestors(t))}updateMatrix(){this.matrix.compose(this.position,this.quaternion,this.scale),this.matrixWorldNeedsUpdate=!0}updateMatrixWorld(t){this.matrixAutoUpdate&&this.updateMatrix(),(this.matrixWorldNeedsUpdate||t)&&(this.matrixWorldAutoUpdate===!0&&(this.parent===null?this.matrixWorld.copy(this.matrix):this.matrixWorld.multiplyMatrices(this.parent.matrixWorld,this.matrix)),this.matrixWorldNeedsUpdate=!1,t=!0);const e=this.children;for(let n=0,i=e.length;n<i;n++)e[n].updateMatrixWorld(t)}updateWorldMatrix(t,e){const n=this.parent;if(t===!0&&n!==null&&n.updateWorldMatrix(!0,!1),this.matrixAutoUpdate&&this.updateMatrix(),this.matrixWorldAutoUpdate===!0&&(this.parent===null?this.matrixWorld.copy(this.matrix):this.matrixWorld.multiplyMatrices(this.parent.matrixWorld,this.matrix)),e===!0){const i=this.children;for(let r=0,a=i.length;r<a;r++)i[r].updateWorldMatrix(!1,!0)}}toJSON(t){const e=t===void 0||typeof t=="string",n={};e&&(t={geometries:{},materials:{},textures:{},images:{},shapes:{},skeletons:{},animations:{},nodes:{}},n.metadata={version:4.7,type:"Object",generator:"Object3D.toJSON"});const i={};i.uuid=this.uuid,i.type=this.type,this.name!==""&&(i.name=this.name),this.castShadow===!0&&(i.castShadow=!0),this.receiveShadow===!0&&(i.receiveShadow=!0),this.visible===!1&&(i.visible=!1),this.frustumCulled===!1&&(i.frustumCulled=!1),this.renderOrder!==0&&(i.renderOrder=this.renderOrder),Object.keys(this.userData).length>0&&(i.userData=this.userData),i.layers=this.layers.mask,i.matrix=this.matrix.toArray(),i.up=this.up.toArray(),this.matrixAutoUpdate===!1&&(i.matrixAutoUpdate=!1),this.isInstancedMesh&&(i.type="InstancedMesh",i.count=this.count,i.instanceMatrix=this.instanceMatrix.toJSON(),this.instanceColor!==null&&(i.instanceColor=this.instanceColor.toJSON())),this.isBatchedMesh&&(i.type="BatchedMesh",i.perObjectFrustumCulled=this.perObjectFrustumCulled,i.sortObjects=this.sortObjects,i.drawRanges=this._drawRanges,i.reservedRanges=this._reservedRanges,i.geometryInfo=this._geometryInfo.map(o=>({...o,boundingBox:o.boundingBox?o.boundingBox.toJSON():void 0,boundingSphere:o.boundingSphere?o.boundingSphere.toJSON():void 0})),i.instanceInfo=this._instanceInfo.map(o=>({...o})),i.availableInstanceIds=this._availableInstanceIds.slice(),i.availableGeometryIds=this._availableGeometryIds.slice(),i.nextIndexStart=this._nextIndexStart,i.nextVertexStart=this._nextVertexStart,i.geometryCount=this._geometryCount,i.maxInstanceCount=this._maxInstanceCount,i.maxVertexCount=this._maxVertexCount,i.maxIndexCount=this._maxIndexCount,i.geometryInitialized=this._geometryInitialized,i.matricesTexture=this._matricesTexture.toJSON(t),i.indirectTexture=this._indirectTexture.toJSON(t),this._colorsTexture!==null&&(i.colorsTexture=this._colorsTexture.toJSON(t)),this.boundingSphere!==null&&(i.boundingSphere=this.boundingSphere.toJSON()),this.boundingBox!==null&&(i.boundingBox=this.boundingBox.toJSON()));function r(o,l){return o[l.uuid]===void 0&&(o[l.uuid]=l.toJSON(t)),l.uuid}if(this.isScene)this.background&&(this.background.isColor?i.background=this.background.toJSON():this.background.isTexture&&(i.background=this.background.toJSON(t).uuid)),this.environment&&this.environment.isTexture&&this.environment.isRenderTargetTexture!==!0&&(i.environment=this.environment.toJSON(t).uuid);else if(this.isMesh||this.isLine||this.isPoints){i.geometry=r(t.geometries,this.geometry);const o=this.geometry.parameters;if(o!==void 0&&o.shapes!==void 0){const l=o.shapes;if(Array.isArray(l))for(let c=0,h=l.length;c<h;c++){const d=l[c];r(t.shapes,d)}else r(t.shapes,l)}}if(this.isSkinnedMesh&&(i.bindMode=this.bindMode,i.bindMatrix=this.bindMatrix.toArray(),this.skeleton!==void 0&&(r(t.skeletons,this.skeleton),i.skeleton=this.skeleton.uuid)),this.material!==void 0)if(Array.isArray(this.material)){const o=[];for(let l=0,c=this.material.length;l<c;l++)o.push(r(t.materials,this.material[l]));i.material=o}else i.material=r(t.materials,this.material);if(this.children.length>0){i.children=[];for(let o=0;o<this.children.length;o++)i.children.push(this.children[o].toJSON(t).object)}if(this.animations.length>0){i.animations=[];for(let o=0;o<this.animations.length;o++){const l=this.animations[o];i.animations.push(r(t.animations,l))}}if(e){const o=a(t.geometries),l=a(t.materials),c=a(t.textures),h=a(t.images),d=a(t.shapes),u=a(t.skeletons),p=a(t.animations),g=a(t.nodes);o.length>0&&(n.geometries=o),l.length>0&&(n.materials=l),c.length>0&&(n.textures=c),h.length>0&&(n.images=h),d.length>0&&(n.shapes=d),u.length>0&&(n.skeletons=u),p.length>0&&(n.animations=p),g.length>0&&(n.nodes=g)}return n.object=i,n;function a(o){const l=[];for(const c in o){const h=o[c];delete h.metadata,l.push(h)}return l}}clone(t){return new this.constructor().copy(this,t)}copy(t,e=!0){if(this.name=t.name,this.up.copy(t.up),this.position.copy(t.position),this.rotation.order=t.rotation.order,this.quaternion.copy(t.quaternion),this.scale.copy(t.scale),this.matrix.copy(t.matrix),this.matrixWorld.copy(t.matrixWorld),this.matrixAutoUpdate=t.matrixAutoUpdate,this.matrixWorldAutoUpdate=t.matrixWorldAutoUpdate,this.matrixWorldNeedsUpdate=t.matrixWorldNeedsUpdate,this.layers.mask=t.layers.mask,this.visible=t.visible,this.castShadow=t.castShadow,this.receiveShadow=t.receiveShadow,this.frustumCulled=t.frustumCulled,this.renderOrder=t.renderOrder,this.animations=t.animations.slice(),this.userData=JSON.parse(JSON.stringify(t.userData)),e===!0)for(let n=0;n<t.children.length;n++){const i=t.children[n];this.add(i.clone())}return this}}fe.DEFAULT_UP=new P(0,1,0);fe.DEFAULT_MATRIX_AUTO_UPDATE=!0;fe.DEFAULT_MATRIX_WORLD_AUTO_UPDATE=!0;const je=new P,pn=new P,sr=new P,mn=new P,ci=new P,hi=new P,mo=new P,rr=new P,ar=new P,or=new P,lr=new Jt,cr=new Jt,hr=new Jt;class Ke{constructor(t=new P,e=new P,n=new P){this.a=t,this.b=e,this.c=n}static getNormal(t,e,n,i){i.subVectors(n,e),je.subVectors(t,e),i.cross(je);const r=i.lengthSq();return r>0?i.multiplyScalar(1/Math.sqrt(r)):i.set(0,0,0)}static getBarycoord(t,e,n,i,r){je.subVectors(i,e),pn.subVectors(n,e),sr.subVectors(t,e);const a=je.dot(je),o=je.dot(pn),l=je.dot(sr),c=pn.dot(pn),h=pn.dot(sr),d=a*c-o*o;if(d===0)return r.set(0,0,0),null;const u=1/d,p=(c*l-o*h)*u,g=(a*h-o*l)*u;return r.set(1-p-g,g,p)}static containsPoint(t,e,n,i){return this.getBarycoord(t,e,n,i,mn)===null?!1:mn.x>=0&&mn.y>=0&&mn.x+mn.y<=1}static getInterpolation(t,e,n,i,r,a,o,l){return this.getBarycoord(t,e,n,i,mn)===null?(l.x=0,l.y=0,"z"in l&&(l.z=0),"w"in l&&(l.w=0),null):(l.setScalar(0),l.addScaledVector(r,mn.x),l.addScaledVector(a,mn.y),l.addScaledVector(o,mn.z),l)}static getInterpolatedAttribute(t,e,n,i,r,a){return lr.setScalar(0),cr.setScalar(0),hr.setScalar(0),lr.fromBufferAttribute(t,e),cr.fromBufferAttribute(t,n),hr.fromBufferAttribute(t,i),a.setScalar(0),a.addScaledVector(lr,r.x),a.addScaledVector(cr,r.y),a.addScaledVector(hr,r.z),a}static isFrontFacing(t,e,n,i){return je.subVectors(n,e),pn.subVectors(t,e),je.cross(pn).dot(i)<0}set(t,e,n){return this.a.copy(t),this.b.copy(e),this.c.copy(n),this}setFromPointsAndIndices(t,e,n,i){return this.a.copy(t[e]),this.b.copy(t[n]),this.c.copy(t[i]),this}setFromAttributeAndIndices(t,e,n,i){return this.a.fromBufferAttribute(t,e),this.b.fromBufferAttribute(t,n),this.c.fromBufferAttribute(t,i),this}clone(){return new this.constructor().copy(this)}copy(t){return this.a.copy(t.a),this.b.copy(t.b),this.c.copy(t.c),this}getArea(){return je.subVectors(this.c,this.b),pn.subVectors(this.a,this.b),je.cross(pn).length()*.5}getMidpoint(t){return t.addVectors(this.a,this.b).add(this.c).multiplyScalar(1/3)}getNormal(t){return Ke.getNormal(this.a,this.b,this.c,t)}getPlane(t){return t.setFromCoplanarPoints(this.a,this.b,this.c)}getBarycoord(t,e){return Ke.getBarycoord(t,this.a,this.b,this.c,e)}getInterpolation(t,e,n,i,r){return Ke.getInterpolation(t,this.a,this.b,this.c,e,n,i,r)}containsPoint(t){return Ke.containsPoint(t,this.a,this.b,this.c)}isFrontFacing(t){return Ke.isFrontFacing(this.a,this.b,this.c,t)}intersectsBox(t){return t.intersectsTriangle(this)}closestPointToPoint(t,e){const n=this.a,i=this.b,r=this.c;let a,o;ci.subVectors(i,n),hi.subVectors(r,n),rr.subVectors(t,n);const l=ci.dot(rr),c=hi.dot(rr);if(l<=0&&c<=0)return e.copy(n);ar.subVectors(t,i);const h=ci.dot(ar),d=hi.dot(ar);if(h>=0&&d<=h)return e.copy(i);const u=l*d-h*c;if(u<=0&&l>=0&&h<=0)return a=l/(l-h),e.copy(n).addScaledVector(ci,a);or.subVectors(t,r);const p=ci.dot(or),g=hi.dot(or);if(g>=0&&p<=g)return e.copy(r);const v=p*c-l*g;if(v<=0&&c>=0&&g<=0)return o=c/(c-g),e.copy(n).addScaledVector(hi,o);const m=h*g-p*d;if(m<=0&&d-h>=0&&p-g>=0)return mo.subVectors(r,i),o=(d-h)/(d-h+(p-g)),e.copy(i).addScaledVector(mo,o);const f=1/(m+v+u);return a=v*f,o=u*f,e.copy(n).addScaledVector(ci,a).addScaledVector(hi,o)}equals(t){return t.a.equals(this.a)&&t.b.equals(this.b)&&t.c.equals(this.c)}}const Nl={aliceblue:15792383,antiquewhite:16444375,aqua:65535,aquamarine:8388564,azure:15794175,beige:16119260,bisque:16770244,black:0,blanchedalmond:16772045,blue:255,blueviolet:9055202,brown:10824234,burlywood:14596231,cadetblue:6266528,chartreuse:8388352,chocolate:13789470,coral:16744272,cornflowerblue:6591981,cornsilk:16775388,crimson:14423100,cyan:65535,darkblue:139,darkcyan:35723,darkgoldenrod:12092939,darkgray:11119017,darkgreen:25600,darkgrey:11119017,darkkhaki:12433259,darkmagenta:9109643,darkolivegreen:5597999,darkorange:16747520,darkorchid:10040012,darkred:9109504,darksalmon:15308410,darkseagreen:9419919,darkslateblue:4734347,darkslategray:3100495,darkslategrey:3100495,darkturquoise:52945,darkviolet:9699539,deeppink:16716947,deepskyblue:49151,dimgray:6908265,dimgrey:6908265,dodgerblue:2003199,firebrick:11674146,floralwhite:16775920,forestgreen:2263842,fuchsia:16711935,gainsboro:14474460,ghostwhite:16316671,gold:16766720,goldenrod:14329120,gray:8421504,green:32768,greenyellow:11403055,grey:8421504,honeydew:15794160,hotpink:16738740,indianred:13458524,indigo:4915330,ivory:16777200,khaki:15787660,lavender:15132410,lavenderblush:16773365,lawngreen:8190976,lemonchiffon:16775885,lightblue:11393254,lightcoral:15761536,lightcyan:14745599,lightgoldenrodyellow:16448210,lightgray:13882323,lightgreen:9498256,lightgrey:13882323,lightpink:16758465,lightsalmon:16752762,lightseagreen:2142890,lightskyblue:8900346,lightslategray:7833753,lightslategrey:7833753,lightsteelblue:11584734,lightyellow:16777184,lime:65280,limegreen:3329330,linen:16445670,magenta:16711935,maroon:8388608,mediumaquamarine:6737322,mediumblue:205,mediumorchid:12211667,mediumpurple:9662683,mediumseagreen:3978097,mediumslateblue:8087790,mediumspringgreen:64154,mediumturquoise:4772300,mediumvioletred:13047173,midnightblue:1644912,mintcream:16121850,mistyrose:16770273,moccasin:16770229,navajowhite:16768685,navy:128,oldlace:16643558,olive:8421376,olivedrab:7048739,orange:16753920,orangered:16729344,orchid:14315734,palegoldenrod:15657130,palegreen:10025880,paleturquoise:11529966,palevioletred:14381203,papayawhip:16773077,peachpuff:16767673,peru:13468991,pink:16761035,plum:14524637,powderblue:11591910,purple:8388736,rebeccapurple:6697881,red:16711680,rosybrown:12357519,royalblue:4286945,saddlebrown:9127187,salmon:16416882,sandybrown:16032864,seagreen:3050327,seashell:16774638,sienna:10506797,silver:12632256,skyblue:8900331,slateblue:6970061,slategray:7372944,slategrey:7372944,snow:16775930,springgreen:65407,steelblue:4620980,tan:13808780,teal:32896,thistle:14204888,tomato:16737095,turquoise:4251856,violet:15631086,wheat:16113331,white:16777215,whitesmoke:16119285,yellow:16776960,yellowgreen:10145074},wn={h:0,s:0,l:0},cs={h:0,s:0,l:0};function ur(s,t,e){return e<0&&(e+=1),e>1&&(e-=1),e<1/6?s+(t-s)*6*e:e<1/2?t:e<2/3?s+(t-s)*6*(2/3-e):s}class Ft{constructor(t,e,n){return this.isColor=!0,this.r=1,this.g=1,this.b=1,this.set(t,e,n)}set(t,e,n){if(e===void 0&&n===void 0){const i=t;i&&i.isColor?this.copy(i):typeof i=="number"?this.setHex(i):typeof i=="string"&&this.setStyle(i)}else this.setRGB(t,e,n);return this}setScalar(t){return this.r=t,this.g=t,this.b=t,this}setHex(t,e=Be){return t=Math.floor(t),this.r=(t>>16&255)/255,this.g=(t>>8&255)/255,this.b=(t&255)/255,Gt.colorSpaceToWorking(this,e),this}setRGB(t,e,n,i=Gt.workingColorSpace){return this.r=t,this.g=e,this.b=n,Gt.colorSpaceToWorking(this,i),this}setHSL(t,e,n,i=Gt.workingColorSpace){if(t=Pa(t,1),e=zt(e,0,1),n=zt(n,0,1),e===0)this.r=this.g=this.b=n;else{const r=n<=.5?n*(1+e):n+e-n*e,a=2*n-r;this.r=ur(a,r,t+1/3),this.g=ur(a,r,t),this.b=ur(a,r,t-1/3)}return Gt.colorSpaceToWorking(this,i),this}setStyle(t,e=Be){function n(r){r!==void 0&&parseFloat(r)<1&&console.warn("THREE.Color: Alpha component of "+t+" will be ignored.")}let i;if(i=/^(\w+)\(([^\)]*)\)/.exec(t)){let r;const a=i[1],o=i[2];switch(a){case"rgb":case"rgba":if(r=/^\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*(?:,\s*(\d*\.?\d+)\s*)?$/.exec(o))return n(r[4]),this.setRGB(Math.min(255,parseInt(r[1],10))/255,Math.min(255,parseInt(r[2],10))/255,Math.min(255,parseInt(r[3],10))/255,e);if(r=/^\s*(\d+)\%\s*,\s*(\d+)\%\s*,\s*(\d+)\%\s*(?:,\s*(\d*\.?\d+)\s*)?$/.exec(o))return n(r[4]),this.setRGB(Math.min(100,parseInt(r[1],10))/100,Math.min(100,parseInt(r[2],10))/100,Math.min(100,parseInt(r[3],10))/100,e);break;case"hsl":case"hsla":if(r=/^\s*(\d*\.?\d+)\s*,\s*(\d*\.?\d+)\%\s*,\s*(\d*\.?\d+)\%\s*(?:,\s*(\d*\.?\d+)\s*)?$/.exec(o))return n(r[4]),this.setHSL(parseFloat(r[1])/360,parseFloat(r[2])/100,parseFloat(r[3])/100,e);break;default:console.warn("THREE.Color: Unknown color model "+t)}}else if(i=/^\#([A-Fa-f\d]+)$/.exec(t)){const r=i[1],a=r.length;if(a===3)return this.setRGB(parseInt(r.charAt(0),16)/15,parseInt(r.charAt(1),16)/15,parseInt(r.charAt(2),16)/15,e);if(a===6)return this.setHex(parseInt(r,16),e);console.warn("THREE.Color: Invalid hex color "+t)}else if(t&&t.length>0)return this.setColorName(t,e);return this}setColorName(t,e=Be){const n=Nl[t.toLowerCase()];return n!==void 0?this.setHex(n,e):console.warn("THREE.Color: Unknown color "+t),this}clone(){return new this.constructor(this.r,this.g,this.b)}copy(t){return this.r=t.r,this.g=t.g,this.b=t.b,this}copySRGBToLinear(t){return this.r=Mn(t.r),this.g=Mn(t.g),this.b=Mn(t.b),this}copyLinearToSRGB(t){return this.r=vi(t.r),this.g=vi(t.g),this.b=vi(t.b),this}convertSRGBToLinear(){return this.copySRGBToLinear(this),this}convertLinearToSRGB(){return this.copyLinearToSRGB(this),this}getHex(t=Be){return Gt.workingToColorSpace(be.copy(this),t),Math.round(zt(be.r*255,0,255))*65536+Math.round(zt(be.g*255,0,255))*256+Math.round(zt(be.b*255,0,255))}getHexString(t=Be){return("000000"+this.getHex(t).toString(16)).slice(-6)}getHSL(t,e=Gt.workingColorSpace){Gt.workingToColorSpace(be.copy(this),e);const n=be.r,i=be.g,r=be.b,a=Math.max(n,i,r),o=Math.min(n,i,r);let l,c;const h=(o+a)/2;if(o===a)l=0,c=0;else{const d=a-o;switch(c=h<=.5?d/(a+o):d/(2-a-o),a){case n:l=(i-r)/d+(i<r?6:0);break;case i:l=(r-n)/d+2;break;case r:l=(n-i)/d+4;break}l/=6}return t.h=l,t.s=c,t.l=h,t}getRGB(t,e=Gt.workingColorSpace){return Gt.workingToColorSpace(be.copy(this),e),t.r=be.r,t.g=be.g,t.b=be.b,t}getStyle(t=Be){Gt.workingToColorSpace(be.copy(this),t);const e=be.r,n=be.g,i=be.b;return t!==Be?`color(${t} ${e.toFixed(3)} ${n.toFixed(3)} ${i.toFixed(3)})`:`rgb(${Math.round(e*255)},${Math.round(n*255)},${Math.round(i*255)})`}offsetHSL(t,e,n){return this.getHSL(wn),this.setHSL(wn.h+t,wn.s+e,wn.l+n)}add(t){return this.r+=t.r,this.g+=t.g,this.b+=t.b,this}addColors(t,e){return this.r=t.r+e.r,this.g=t.g+e.g,this.b=t.b+e.b,this}addScalar(t){return this.r+=t,this.g+=t,this.b+=t,this}sub(t){return this.r=Math.max(0,this.r-t.r),this.g=Math.max(0,this.g-t.g),this.b=Math.max(0,this.b-t.b),this}multiply(t){return this.r*=t.r,this.g*=t.g,this.b*=t.b,this}multiplyScalar(t){return this.r*=t,this.g*=t,this.b*=t,this}lerp(t,e){return this.r+=(t.r-this.r)*e,this.g+=(t.g-this.g)*e,this.b+=(t.b-this.b)*e,this}lerpColors(t,e,n){return this.r=t.r+(e.r-t.r)*n,this.g=t.g+(e.g-t.g)*n,this.b=t.b+(e.b-t.b)*n,this}lerpHSL(t,e){this.getHSL(wn),t.getHSL(cs);const n=Hi(wn.h,cs.h,e),i=Hi(wn.s,cs.s,e),r=Hi(wn.l,cs.l,e);return this.setHSL(n,i,r),this}setFromVector3(t){return this.r=t.x,this.g=t.y,this.b=t.z,this}applyMatrix3(t){const e=this.r,n=this.g,i=this.b,r=t.elements;return this.r=r[0]*e+r[3]*n+r[6]*i,this.g=r[1]*e+r[4]*n+r[7]*i,this.b=r[2]*e+r[5]*n+r[8]*i,this}equals(t){return t.r===this.r&&t.g===this.g&&t.b===this.b}fromArray(t,e=0){return this.r=t[e],this.g=t[e+1],this.b=t[e+2],this}toArray(t=[],e=0){return t[e]=this.r,t[e+1]=this.g,t[e+2]=this.b,t}fromBufferAttribute(t,e){return this.r=t.getX(e),this.g=t.getY(e),this.b=t.getZ(e),this}toJSON(){return this.getHex()}*[Symbol.iterator](){yield this.r,yield this.g,yield this.b}}const be=new Ft;Ft.NAMES=Nl;let gh=0;class Qn extends Zn{constructor(){super(),this.isMaterial=!0,Object.defineProperty(this,"id",{value:gh++}),this.uuid=wi(),this.name="",this.type="Material",this.blending=xi,this.side=Dn,this.vertexColors=!1,this.opacity=1,this.transparent=!1,this.alphaHash=!1,this.blendSrc=Lr,this.blendDst=Dr,this.blendEquation=Vn,this.blendSrcAlpha=null,this.blendDstAlpha=null,this.blendEquationAlpha=null,this.blendColor=new Ft(0,0,0),this.blendAlpha=0,this.depthFunc=yi,this.depthTest=!0,this.depthWrite=!0,this.stencilWriteMask=255,this.stencilFunc=Qa,this.stencilRef=0,this.stencilFuncMask=255,this.stencilFail=ei,this.stencilZFail=ei,this.stencilZPass=ei,this.stencilWrite=!1,this.clippingPlanes=null,this.clipIntersection=!1,this.clipShadows=!1,this.shadowSide=null,this.colorWrite=!0,this.precision=null,this.polygonOffset=!1,this.polygonOffsetFactor=0,this.polygonOffsetUnits=0,this.dithering=!1,this.alphaToCoverage=!1,this.premultipliedAlpha=!1,this.forceSinglePass=!1,this.allowOverride=!0,this.visible=!0,this.toneMapped=!0,this.userData={},this.version=0,this._alphaTest=0}get alphaTest(){return this._alphaTest}set alphaTest(t){this._alphaTest>0!=t>0&&this.version++,this._alphaTest=t}onBeforeRender(){}onBeforeCompile(){}customProgramCacheKey(){return this.onBeforeCompile.toString()}setValues(t){if(t!==void 0)for(const e in t){const n=t[e];if(n===void 0){console.warn(`THREE.Material: parameter '${e}' has value of undefined.`);continue}const i=this[e];if(i===void 0){console.warn(`THREE.Material: '${e}' is not a property of THREE.${this.type}.`);continue}i&&i.isColor?i.set(n):i&&i.isVector3&&n&&n.isVector3?i.copy(n):this[e]=n}}toJSON(t){const e=t===void 0||typeof t=="string";e&&(t={textures:{},images:{}});const n={metadata:{version:4.7,type:"Material",generator:"Material.toJSON"}};n.uuid=this.uuid,n.type=this.type,this.name!==""&&(n.name=this.name),this.color&&this.color.isColor&&(n.color=this.color.getHex()),this.roughness!==void 0&&(n.roughness=this.roughness),this.metalness!==void 0&&(n.metalness=this.metalness),this.sheen!==void 0&&(n.sheen=this.sheen),this.sheenColor&&this.sheenColor.isColor&&(n.sheenColor=this.sheenColor.getHex()),this.sheenRoughness!==void 0&&(n.sheenRoughness=this.sheenRoughness),this.emissive&&this.emissive.isColor&&(n.emissive=this.emissive.getHex()),this.emissiveIntensity!==void 0&&this.emissiveIntensity!==1&&(n.emissiveIntensity=this.emissiveIntensity),this.specular&&this.specular.isColor&&(n.specular=this.specular.getHex()),this.specularIntensity!==void 0&&(n.specularIntensity=this.specularIntensity),this.specularColor&&this.specularColor.isColor&&(n.specularColor=this.specularColor.getHex()),this.shininess!==void 0&&(n.shininess=this.shininess),this.clearcoat!==void 0&&(n.clearcoat=this.clearcoat),this.clearcoatRoughness!==void 0&&(n.clearcoatRoughness=this.clearcoatRoughness),this.clearcoatMap&&this.clearcoatMap.isTexture&&(n.clearcoatMap=this.clearcoatMap.toJSON(t).uuid),this.clearcoatRoughnessMap&&this.clearcoatRoughnessMap.isTexture&&(n.clearcoatRoughnessMap=this.clearcoatRoughnessMap.toJSON(t).uuid),this.clearcoatNormalMap&&this.clearcoatNormalMap.isTexture&&(n.clearcoatNormalMap=this.clearcoatNormalMap.toJSON(t).uuid,n.clearcoatNormalScale=this.clearcoatNormalScale.toArray()),this.sheenColorMap&&this.sheenColorMap.isTexture&&(n.sheenColorMap=this.sheenColorMap.toJSON(t).uuid),this.sheenRoughnessMap&&this.sheenRoughnessMap.isTexture&&(n.sheenRoughnessMap=this.sheenRoughnessMap.toJSON(t).uuid),this.dispersion!==void 0&&(n.dispersion=this.dispersion),this.iridescence!==void 0&&(n.iridescence=this.iridescence),this.iridescenceIOR!==void 0&&(n.iridescenceIOR=this.iridescenceIOR),this.iridescenceThicknessRange!==void 0&&(n.iridescenceThicknessRange=this.iridescenceThicknessRange),this.iridescenceMap&&this.iridescenceMap.isTexture&&(n.iridescenceMap=this.iridescenceMap.toJSON(t).uuid),this.iridescenceThicknessMap&&this.iridescenceThicknessMap.isTexture&&(n.iridescenceThicknessMap=this.iridescenceThicknessMap.toJSON(t).uuid),this.anisotropy!==void 0&&(n.anisotropy=this.anisotropy),this.anisotropyRotation!==void 0&&(n.anisotropyRotation=this.anisotropyRotation),this.anisotropyMap&&this.anisotropyMap.isTexture&&(n.anisotropyMap=this.anisotropyMap.toJSON(t).uuid),this.map&&this.map.isTexture&&(n.map=this.map.toJSON(t).uuid),this.matcap&&this.matcap.isTexture&&(n.matcap=this.matcap.toJSON(t).uuid),this.alphaMap&&this.alphaMap.isTexture&&(n.alphaMap=this.alphaMap.toJSON(t).uuid),this.lightMap&&this.lightMap.isTexture&&(n.lightMap=this.lightMap.toJSON(t).uuid,n.lightMapIntensity=this.lightMapIntensity),this.aoMap&&this.aoMap.isTexture&&(n.aoMap=this.aoMap.toJSON(t).uuid,n.aoMapIntensity=this.aoMapIntensity),this.bumpMap&&this.bumpMap.isTexture&&(n.bumpMap=this.bumpMap.toJSON(t).uuid,n.bumpScale=this.bumpScale),this.normalMap&&this.normalMap.isTexture&&(n.normalMap=this.normalMap.toJSON(t).uuid,n.normalMapType=this.normalMapType,n.normalScale=this.normalScale.toArray()),this.displacementMap&&this.displacementMap.isTexture&&(n.displacementMap=this.displacementMap.toJSON(t).uuid,n.displacementScale=this.displacementScale,n.displacementBias=this.displacementBias),this.roughnessMap&&this.roughnessMap.isTexture&&(n.roughnessMap=this.roughnessMap.toJSON(t).uuid),this.metalnessMap&&this.metalnessMap.isTexture&&(n.metalnessMap=this.metalnessMap.toJSON(t).uuid),this.emissiveMap&&this.emissiveMap.isTexture&&(n.emissiveMap=this.emissiveMap.toJSON(t).uuid),this.specularMap&&this.specularMap.isTexture&&(n.specularMap=this.specularMap.toJSON(t).uuid),this.specularIntensityMap&&this.specularIntensityMap.isTexture&&(n.specularIntensityMap=this.specularIntensityMap.toJSON(t).uuid),this.specularColorMap&&this.specularColorMap.isTexture&&(n.specularColorMap=this.specularColorMap.toJSON(t).uuid),this.envMap&&this.envMap.isTexture&&(n.envMap=this.envMap.toJSON(t).uuid,this.combine!==void 0&&(n.combine=this.combine)),this.envMapRotation!==void 0&&(n.envMapRotation=this.envMapRotation.toArray()),this.envMapIntensity!==void 0&&(n.envMapIntensity=this.envMapIntensity),this.reflectivity!==void 0&&(n.reflectivity=this.reflectivity),this.refractionRatio!==void 0&&(n.refractionRatio=this.refractionRatio),this.gradientMap&&this.gradientMap.isTexture&&(n.gradientMap=this.gradientMap.toJSON(t).uuid),this.transmission!==void 0&&(n.transmission=this.transmission),this.transmissionMap&&this.transmissionMap.isTexture&&(n.transmissionMap=this.transmissionMap.toJSON(t).uuid),this.thickness!==void 0&&(n.thickness=this.thickness),this.thicknessMap&&this.thicknessMap.isTexture&&(n.thicknessMap=this.thicknessMap.toJSON(t).uuid),this.attenuationDistance!==void 0&&this.attenuationDistance!==1/0&&(n.attenuationDistance=this.attenuationDistance),this.attenuationColor!==void 0&&(n.attenuationColor=this.attenuationColor.getHex()),this.size!==void 0&&(n.size=this.size),this.shadowSide!==null&&(n.shadowSide=this.shadowSide),this.sizeAttenuation!==void 0&&(n.sizeAttenuation=this.sizeAttenuation),this.blending!==xi&&(n.blending=this.blending),this.side!==Dn&&(n.side=this.side),this.vertexColors===!0&&(n.vertexColors=!0),this.opacity<1&&(n.opacity=this.opacity),this.transparent===!0&&(n.transparent=!0),this.blendSrc!==Lr&&(n.blendSrc=this.blendSrc),this.blendDst!==Dr&&(n.blendDst=this.blendDst),this.blendEquation!==Vn&&(n.blendEquation=this.blendEquation),this.blendSrcAlpha!==null&&(n.blendSrcAlpha=this.blendSrcAlpha),this.blendDstAlpha!==null&&(n.blendDstAlpha=this.blendDstAlpha),this.blendEquationAlpha!==null&&(n.blendEquationAlpha=this.blendEquationAlpha),this.blendColor&&this.blendColor.isColor&&(n.blendColor=this.blendColor.getHex()),this.blendAlpha!==0&&(n.blendAlpha=this.blendAlpha),this.depthFunc!==yi&&(n.depthFunc=this.depthFunc),this.depthTest===!1&&(n.depthTest=this.depthTest),this.depthWrite===!1&&(n.depthWrite=this.depthWrite),this.colorWrite===!1&&(n.colorWrite=this.colorWrite),this.stencilWriteMask!==255&&(n.stencilWriteMask=this.stencilWriteMask),this.stencilFunc!==Qa&&(n.stencilFunc=this.stencilFunc),this.stencilRef!==0&&(n.stencilRef=this.stencilRef),this.stencilFuncMask!==255&&(n.stencilFuncMask=this.stencilFuncMask),this.stencilFail!==ei&&(n.stencilFail=this.stencilFail),this.stencilZFail!==ei&&(n.stencilZFail=this.stencilZFail),this.stencilZPass!==ei&&(n.stencilZPass=this.stencilZPass),this.stencilWrite===!0&&(n.stencilWrite=this.stencilWrite),this.rotation!==void 0&&this.rotation!==0&&(n.rotation=this.rotation),this.polygonOffset===!0&&(n.polygonOffset=!0),this.polygonOffsetFactor!==0&&(n.polygonOffsetFactor=this.polygonOffsetFactor),this.polygonOffsetUnits!==0&&(n.polygonOffsetUnits=this.polygonOffsetUnits),this.linewidth!==void 0&&this.linewidth!==1&&(n.linewidth=this.linewidth),this.dashSize!==void 0&&(n.dashSize=this.dashSize),this.gapSize!==void 0&&(n.gapSize=this.gapSize),this.scale!==void 0&&(n.scale=this.scale),this.dithering===!0&&(n.dithering=!0),this.alphaTest>0&&(n.alphaTest=this.alphaTest),this.alphaHash===!0&&(n.alphaHash=!0),this.alphaToCoverage===!0&&(n.alphaToCoverage=!0),this.premultipliedAlpha===!0&&(n.premultipliedAlpha=!0),this.forceSinglePass===!0&&(n.forceSinglePass=!0),this.wireframe===!0&&(n.wireframe=!0),this.wireframeLinewidth>1&&(n.wireframeLinewidth=this.wireframeLinewidth),this.wireframeLinecap!=="round"&&(n.wireframeLinecap=this.wireframeLinecap),this.wireframeLinejoin!=="round"&&(n.wireframeLinejoin=this.wireframeLinejoin),this.flatShading===!0&&(n.flatShading=!0),this.visible===!1&&(n.visible=!1),this.toneMapped===!1&&(n.toneMapped=!1),this.fog===!1&&(n.fog=!1),Object.keys(this.userData).length>0&&(n.userData=this.userData);function i(r){const a=[];for(const o in r){const l=r[o];delete l.metadata,a.push(l)}return a}if(e){const r=i(t.textures),a=i(t.images);r.length>0&&(n.textures=r),a.length>0&&(n.images=a)}return n}clone(){return new this.constructor().copy(this)}copy(t){this.name=t.name,this.blending=t.blending,this.side=t.side,this.vertexColors=t.vertexColors,this.opacity=t.opacity,this.transparent=t.transparent,this.blendSrc=t.blendSrc,this.blendDst=t.blendDst,this.blendEquation=t.blendEquation,this.blendSrcAlpha=t.blendSrcAlpha,this.blendDstAlpha=t.blendDstAlpha,this.blendEquationAlpha=t.blendEquationAlpha,this.blendColor.copy(t.blendColor),this.blendAlpha=t.blendAlpha,this.depthFunc=t.depthFunc,this.depthTest=t.depthTest,this.depthWrite=t.depthWrite,this.stencilWriteMask=t.stencilWriteMask,this.stencilFunc=t.stencilFunc,this.stencilRef=t.stencilRef,this.stencilFuncMask=t.stencilFuncMask,this.stencilFail=t.stencilFail,this.stencilZFail=t.stencilZFail,this.stencilZPass=t.stencilZPass,this.stencilWrite=t.stencilWrite;const e=t.clippingPlanes;let n=null;if(e!==null){const i=e.length;n=new Array(i);for(let r=0;r!==i;++r)n[r]=e[r].clone()}return this.clippingPlanes=n,this.clipIntersection=t.clipIntersection,this.clipShadows=t.clipShadows,this.shadowSide=t.shadowSide,this.colorWrite=t.colorWrite,this.precision=t.precision,this.polygonOffset=t.polygonOffset,this.polygonOffsetFactor=t.polygonOffsetFactor,this.polygonOffsetUnits=t.polygonOffsetUnits,this.dithering=t.dithering,this.alphaTest=t.alphaTest,this.alphaHash=t.alphaHash,this.alphaToCoverage=t.alphaToCoverage,this.premultipliedAlpha=t.premultipliedAlpha,this.forceSinglePass=t.forceSinglePass,this.visible=t.visible,this.toneMapped=t.toneMapped,this.userData=JSON.parse(JSON.stringify(t.userData)),this}dispose(){this.dispatchEvent({type:"dispose"})}set needsUpdate(t){t===!0&&this.version++}}class qn extends Qn{constructor(t){super(),this.isMeshBasicMaterial=!0,this.type="MeshBasicMaterial",this.color=new Ft(16777215),this.map=null,this.lightMap=null,this.lightMapIntensity=1,this.aoMap=null,this.aoMapIntensity=1,this.specularMap=null,this.alphaMap=null,this.envMap=null,this.envMapRotation=new cn,this.combine=gl,this.reflectivity=1,this.refractionRatio=.98,this.wireframe=!1,this.wireframeLinewidth=1,this.wireframeLinecap="round",this.wireframeLinejoin="round",this.fog=!0,this.setValues(t)}copy(t){return super.copy(t),this.color.copy(t.color),this.map=t.map,this.lightMap=t.lightMap,this.lightMapIntensity=t.lightMapIntensity,this.aoMap=t.aoMap,this.aoMapIntensity=t.aoMapIntensity,this.specularMap=t.specularMap,this.alphaMap=t.alphaMap,this.envMap=t.envMap,this.envMapRotation.copy(t.envMapRotation),this.combine=t.combine,this.reflectivity=t.reflectivity,this.refractionRatio=t.refractionRatio,this.wireframe=t.wireframe,this.wireframeLinewidth=t.wireframeLinewidth,this.wireframeLinecap=t.wireframeLinecap,this.wireframeLinejoin=t.wireframeLinejoin,this.fog=t.fog,this}}const de=new P,hs=new dt;let _h=0;class Xe{constructor(t,e,n=!1){if(Array.isArray(t))throw new TypeError("THREE.BufferAttribute: array should be a Typed Array.");this.isBufferAttribute=!0,Object.defineProperty(this,"id",{value:_h++}),this.name="",this.array=t,this.itemSize=e,this.count=t!==void 0?t.length/e:0,this.normalized=n,this.usage=to,this.updateRanges=[],this.gpuType=an,this.version=0}onUploadCallback(){}set needsUpdate(t){t===!0&&this.version++}setUsage(t){return this.usage=t,this}addUpdateRange(t,e){this.updateRanges.push({start:t,count:e})}clearUpdateRanges(){this.updateRanges.length=0}copy(t){return this.name=t.name,this.array=new t.array.constructor(t.array),this.itemSize=t.itemSize,this.count=t.count,this.normalized=t.normalized,this.usage=t.usage,this.gpuType=t.gpuType,this}copyAt(t,e,n){t*=this.itemSize,n*=e.itemSize;for(let i=0,r=this.itemSize;i<r;i++)this.array[t+i]=e.array[n+i];return this}copyArray(t){return this.array.set(t),this}applyMatrix3(t){if(this.itemSize===2)for(let e=0,n=this.count;e<n;e++)hs.fromBufferAttribute(this,e),hs.applyMatrix3(t),this.setXY(e,hs.x,hs.y);else if(this.itemSize===3)for(let e=0,n=this.count;e<n;e++)de.fromBufferAttribute(this,e),de.applyMatrix3(t),this.setXYZ(e,de.x,de.y,de.z);return this}applyMatrix4(t){for(let e=0,n=this.count;e<n;e++)de.fromBufferAttribute(this,e),de.applyMatrix4(t),this.setXYZ(e,de.x,de.y,de.z);return this}applyNormalMatrix(t){for(let e=0,n=this.count;e<n;e++)de.fromBufferAttribute(this,e),de.applyNormalMatrix(t),this.setXYZ(e,de.x,de.y,de.z);return this}transformDirection(t){for(let e=0,n=this.count;e<n;e++)de.fromBufferAttribute(this,e),de.transformDirection(t),this.setXYZ(e,de.x,de.y,de.z);return this}set(t,e=0){return this.array.set(t,e),this}getComponent(t,e){let n=this.array[t*this.itemSize+e];return this.normalized&&(n=gi(n,this.array)),n}setComponent(t,e,n){return this.normalized&&(n=we(n,this.array)),this.array[t*this.itemSize+e]=n,this}getX(t){let e=this.array[t*this.itemSize];return this.normalized&&(e=gi(e,this.array)),e}setX(t,e){return this.normalized&&(e=we(e,this.array)),this.array[t*this.itemSize]=e,this}getY(t){let e=this.array[t*this.itemSize+1];return this.normalized&&(e=gi(e,this.array)),e}setY(t,e){return this.normalized&&(e=we(e,this.array)),this.array[t*this.itemSize+1]=e,this}getZ(t){let e=this.array[t*this.itemSize+2];return this.normalized&&(e=gi(e,this.array)),e}setZ(t,e){return this.normalized&&(e=we(e,this.array)),this.array[t*this.itemSize+2]=e,this}getW(t){let e=this.array[t*this.itemSize+3];return this.normalized&&(e=gi(e,this.array)),e}setW(t,e){return this.normalized&&(e=we(e,this.array)),this.array[t*this.itemSize+3]=e,this}setXY(t,e,n){return t*=this.itemSize,this.normalized&&(e=we(e,this.array),n=we(n,this.array)),this.array[t+0]=e,this.array[t+1]=n,this}setXYZ(t,e,n,i){return t*=this.itemSize,this.normalized&&(e=we(e,this.array),n=we(n,this.array),i=we(i,this.array)),this.array[t+0]=e,this.array[t+1]=n,this.array[t+2]=i,this}setXYZW(t,e,n,i,r){return t*=this.itemSize,this.normalized&&(e=we(e,this.array),n=we(n,this.array),i=we(i,this.array),r=we(r,this.array)),this.array[t+0]=e,this.array[t+1]=n,this.array[t+2]=i,this.array[t+3]=r,this}onUpload(t){return this.onUploadCallback=t,this}clone(){return new this.constructor(this.array,this.itemSize).copy(this)}toJSON(){const t={itemSize:this.itemSize,type:this.array.constructor.name,array:Array.from(this.array),normalized:this.normalized};return this.name!==""&&(t.name=this.name),this.usage!==to&&(t.usage=this.usage),t}}class Fl extends Xe{constructor(t,e,n){super(new Uint16Array(t),e,n)}}class Ol extends Xe{constructor(t,e,n){super(new Uint32Array(t),e,n)}}class Xt extends Xe{constructor(t,e,n){super(new Float32Array(t),e,n)}}let xh=0;const Ve=new Kt,dr=new fe,ui=new P,Oe=new $n,Ii=new $n,ve=new P;class me extends Zn{constructor(){super(),this.isBufferGeometry=!0,Object.defineProperty(this,"id",{value:xh++}),this.uuid=wi(),this.name="",this.type="BufferGeometry",this.index=null,this.indirect=null,this.attributes={},this.morphAttributes={},this.morphTargetsRelative=!1,this.groups=[],this.boundingBox=null,this.boundingSphere=null,this.drawRange={start:0,count:1/0},this.userData={}}getIndex(){return this.index}setIndex(t){return Array.isArray(t)?this.index=new(Il(t)?Ol:Fl)(t,1):this.index=t,this}setIndirect(t){return this.indirect=t,this}getIndirect(){return this.indirect}getAttribute(t){return this.attributes[t]}setAttribute(t,e){return this.attributes[t]=e,this}deleteAttribute(t){return delete this.attributes[t],this}hasAttribute(t){return this.attributes[t]!==void 0}addGroup(t,e,n=0){this.groups.push({start:t,count:e,materialIndex:n})}clearGroups(){this.groups=[]}setDrawRange(t,e){this.drawRange.start=t,this.drawRange.count=e}applyMatrix4(t){const e=this.attributes.position;e!==void 0&&(e.applyMatrix4(t),e.needsUpdate=!0);const n=this.attributes.normal;if(n!==void 0){const r=new Nt().getNormalMatrix(t);n.applyNormalMatrix(r),n.needsUpdate=!0}const i=this.attributes.tangent;return i!==void 0&&(i.transformDirection(t),i.needsUpdate=!0),this.boundingBox!==null&&this.computeBoundingBox(),this.boundingSphere!==null&&this.computeBoundingSphere(),this}applyQuaternion(t){return Ve.makeRotationFromQuaternion(t),this.applyMatrix4(Ve),this}rotateX(t){return Ve.makeRotationX(t),this.applyMatrix4(Ve),this}rotateY(t){return Ve.makeRotationY(t),this.applyMatrix4(Ve),this}rotateZ(t){return Ve.makeRotationZ(t),this.applyMatrix4(Ve),this}translate(t,e,n){return Ve.makeTranslation(t,e,n),this.applyMatrix4(Ve),this}scale(t,e,n){return Ve.makeScale(t,e,n),this.applyMatrix4(Ve),this}lookAt(t){return dr.lookAt(t),dr.updateMatrix(),this.applyMatrix4(dr.matrix),this}center(){return this.computeBoundingBox(),this.boundingBox.getCenter(ui).negate(),this.translate(ui.x,ui.y,ui.z),this}setFromPoints(t){const e=this.getAttribute("position");if(e===void 0){const n=[];for(let i=0,r=t.length;i<r;i++){const a=t[i];n.push(a.x,a.y,a.z||0)}this.setAttribute("position",new Xt(n,3))}else{const n=Math.min(t.length,e.count);for(let i=0;i<n;i++){const r=t[i];e.setXYZ(i,r.x,r.y,r.z||0)}t.length>e.count&&console.warn("THREE.BufferGeometry: Buffer size too small for points data. Use .dispose() and create a new geometry."),e.needsUpdate=!0}return this}computeBoundingBox(){this.boundingBox===null&&(this.boundingBox=new $n);const t=this.attributes.position,e=this.morphAttributes.position;if(t&&t.isGLBufferAttribute){console.error("THREE.BufferGeometry.computeBoundingBox(): GLBufferAttribute requires a manual bounding box.",this),this.boundingBox.set(new P(-1/0,-1/0,-1/0),new P(1/0,1/0,1/0));return}if(t!==void 0){if(this.boundingBox.setFromBufferAttribute(t),e)for(let n=0,i=e.length;n<i;n++){const r=e[n];Oe.setFromBufferAttribute(r),this.morphTargetsRelative?(ve.addVectors(this.boundingBox.min,Oe.min),this.boundingBox.expandByPoint(ve),ve.addVectors(this.boundingBox.max,Oe.max),this.boundingBox.expandByPoint(ve)):(this.boundingBox.expandByPoint(Oe.min),this.boundingBox.expandByPoint(Oe.max))}}else this.boundingBox.makeEmpty();(isNaN(this.boundingBox.min.x)||isNaN(this.boundingBox.min.y)||isNaN(this.boundingBox.min.z))&&console.error('THREE.BufferGeometry.computeBoundingBox(): Computed min/max have NaN values. The "position" attribute is likely to have NaN values.',this)}computeBoundingSphere(){this.boundingSphere===null&&(this.boundingSphere=new Jn);const t=this.attributes.position,e=this.morphAttributes.position;if(t&&t.isGLBufferAttribute){console.error("THREE.BufferGeometry.computeBoundingSphere(): GLBufferAttribute requires a manual bounding sphere.",this),this.boundingSphere.set(new P,1/0);return}if(t){const n=this.boundingSphere.center;if(Oe.setFromBufferAttribute(t),e)for(let r=0,a=e.length;r<a;r++){const o=e[r];Ii.setFromBufferAttribute(o),this.morphTargetsRelative?(ve.addVectors(Oe.min,Ii.min),Oe.expandByPoint(ve),ve.addVectors(Oe.max,Ii.max),Oe.expandByPoint(ve)):(Oe.expandByPoint(Ii.min),Oe.expandByPoint(Ii.max))}Oe.getCenter(n);let i=0;for(let r=0,a=t.count;r<a;r++)ve.fromBufferAttribute(t,r),i=Math.max(i,n.distanceToSquared(ve));if(e)for(let r=0,a=e.length;r<a;r++){const o=e[r],l=this.morphTargetsRelative;for(let c=0,h=o.count;c<h;c++)ve.fromBufferAttribute(o,c),l&&(ui.fromBufferAttribute(t,c),ve.add(ui)),i=Math.max(i,n.distanceToSquared(ve))}this.boundingSphere.radius=Math.sqrt(i),isNaN(this.boundingSphere.radius)&&console.error('THREE.BufferGeometry.computeBoundingSphere(): Computed radius is NaN. The "position" attribute is likely to have NaN values.',this)}}computeTangents(){const t=this.index,e=this.attributes;if(t===null||e.position===void 0||e.normal===void 0||e.uv===void 0){console.error("THREE.BufferGeometry: .computeTangents() failed. Missing required attributes (index, position, normal or uv)");return}const n=e.position,i=e.normal,r=e.uv;this.hasAttribute("tangent")===!1&&this.setAttribute("tangent",new Xe(new Float32Array(4*n.count),4));const a=this.getAttribute("tangent"),o=[],l=[];for(let D=0;D<n.count;D++)o[D]=new P,l[D]=new P;const c=new P,h=new P,d=new P,u=new dt,p=new dt,g=new dt,v=new P,m=new P;function f(D,y,x){c.fromBufferAttribute(n,D),h.fromBufferAttribute(n,y),d.fromBufferAttribute(n,x),u.fromBufferAttribute(r,D),p.fromBufferAttribute(r,y),g.fromBufferAttribute(r,x),h.sub(c),d.sub(c),p.sub(u),g.sub(u);const L=1/(p.x*g.y-g.x*p.y);isFinite(L)&&(v.copy(h).multiplyScalar(g.y).addScaledVector(d,-p.y).multiplyScalar(L),m.copy(d).multiplyScalar(p.x).addScaledVector(h,-g.x).multiplyScalar(L),o[D].add(v),o[y].add(v),o[x].add(v),l[D].add(m),l[y].add(m),l[x].add(m))}let E=this.groups;E.length===0&&(E=[{start:0,count:t.count}]);for(let D=0,y=E.length;D<y;++D){const x=E[D],L=x.start,N=x.count;for(let k=L,X=L+N;k<X;k+=3)f(t.getX(k+0),t.getX(k+1),t.getX(k+2))}const b=new P,S=new P,w=new P,A=new P;function C(D){w.fromBufferAttribute(i,D),A.copy(w);const y=o[D];b.copy(y),b.sub(w.multiplyScalar(w.dot(y))).normalize(),S.crossVectors(A,y);const L=S.dot(l[D])<0?-1:1;a.setXYZW(D,b.x,b.y,b.z,L)}for(let D=0,y=E.length;D<y;++D){const x=E[D],L=x.start,N=x.count;for(let k=L,X=L+N;k<X;k+=3)C(t.getX(k+0)),C(t.getX(k+1)),C(t.getX(k+2))}}computeVertexNormals(){const t=this.index,e=this.getAttribute("position");if(e!==void 0){let n=this.getAttribute("normal");if(n===void 0)n=new Xe(new Float32Array(e.count*3),3),this.setAttribute("normal",n);else for(let u=0,p=n.count;u<p;u++)n.setXYZ(u,0,0,0);const i=new P,r=new P,a=new P,o=new P,l=new P,c=new P,h=new P,d=new P;if(t)for(let u=0,p=t.count;u<p;u+=3){const g=t.getX(u+0),v=t.getX(u+1),m=t.getX(u+2);i.fromBufferAttribute(e,g),r.fromBufferAttribute(e,v),a.fromBufferAttribute(e,m),h.subVectors(a,r),d.subVectors(i,r),h.cross(d),o.fromBufferAttribute(n,g),l.fromBufferAttribute(n,v),c.fromBufferAttribute(n,m),o.add(h),l.add(h),c.add(h),n.setXYZ(g,o.x,o.y,o.z),n.setXYZ(v,l.x,l.y,l.z),n.setXYZ(m,c.x,c.y,c.z)}else for(let u=0,p=e.count;u<p;u+=3)i.fromBufferAttribute(e,u+0),r.fromBufferAttribute(e,u+1),a.fromBufferAttribute(e,u+2),h.subVectors(a,r),d.subVectors(i,r),h.cross(d),n.setXYZ(u+0,h.x,h.y,h.z),n.setXYZ(u+1,h.x,h.y,h.z),n.setXYZ(u+2,h.x,h.y,h.z);this.normalizeNormals(),n.needsUpdate=!0}}normalizeNormals(){const t=this.attributes.normal;for(let e=0,n=t.count;e<n;e++)ve.fromBufferAttribute(t,e),ve.normalize(),t.setXYZ(e,ve.x,ve.y,ve.z)}toNonIndexed(){function t(o,l){const c=o.array,h=o.itemSize,d=o.normalized,u=new c.constructor(l.length*h);let p=0,g=0;for(let v=0,m=l.length;v<m;v++){o.isInterleavedBufferAttribute?p=l[v]*o.data.stride+o.offset:p=l[v]*h;for(let f=0;f<h;f++)u[g++]=c[p++]}return new Xe(u,h,d)}if(this.index===null)return console.warn("THREE.BufferGeometry.toNonIndexed(): BufferGeometry is already non-indexed."),this;const e=new me,n=this.index.array,i=this.attributes;for(const o in i){const l=i[o],c=t(l,n);e.setAttribute(o,c)}const r=this.morphAttributes;for(const o in r){const l=[],c=r[o];for(let h=0,d=c.length;h<d;h++){const u=c[h],p=t(u,n);l.push(p)}e.morphAttributes[o]=l}e.morphTargetsRelative=this.morphTargetsRelative;const a=this.groups;for(let o=0,l=a.length;o<l;o++){const c=a[o];e.addGroup(c.start,c.count,c.materialIndex)}return e}toJSON(){const t={metadata:{version:4.7,type:"BufferGeometry",generator:"BufferGeometry.toJSON"}};if(t.uuid=this.uuid,t.type=this.type,this.name!==""&&(t.name=this.name),Object.keys(this.userData).length>0&&(t.userData=this.userData),this.parameters!==void 0){const l=this.parameters;for(const c in l)l[c]!==void 0&&(t[c]=l[c]);return t}t.data={attributes:{}};const e=this.index;e!==null&&(t.data.index={type:e.array.constructor.name,array:Array.prototype.slice.call(e.array)});const n=this.attributes;for(const l in n){const c=n[l];t.data.attributes[l]=c.toJSON(t.data)}const i={};let r=!1;for(const l in this.morphAttributes){const c=this.morphAttributes[l],h=[];for(let d=0,u=c.length;d<u;d++){const p=c[d];h.push(p.toJSON(t.data))}h.length>0&&(i[l]=h,r=!0)}r&&(t.data.morphAttributes=i,t.data.morphTargetsRelative=this.morphTargetsRelative);const a=this.groups;a.length>0&&(t.data.groups=JSON.parse(JSON.stringify(a)));const o=this.boundingSphere;return o!==null&&(t.data.boundingSphere=o.toJSON()),t}clone(){return new this.constructor().copy(this)}copy(t){this.index=null,this.attributes={},this.morphAttributes={},this.groups=[],this.boundingBox=null,this.boundingSphere=null;const e={};this.name=t.name;const n=t.index;n!==null&&this.setIndex(n.clone());const i=t.attributes;for(const c in i){const h=i[c];this.setAttribute(c,h.clone(e))}const r=t.morphAttributes;for(const c in r){const h=[],d=r[c];for(let u=0,p=d.length;u<p;u++)h.push(d[u].clone(e));this.morphAttributes[c]=h}this.morphTargetsRelative=t.morphTargetsRelative;const a=t.groups;for(let c=0,h=a.length;c<h;c++){const d=a[c];this.addGroup(d.start,d.count,d.materialIndex)}const o=t.boundingBox;o!==null&&(this.boundingBox=o.clone());const l=t.boundingSphere;return l!==null&&(this.boundingSphere=l.clone()),this.drawRange.start=t.drawRange.start,this.drawRange.count=t.drawRange.count,this.userData=t.userData,this}dispose(){this.dispatchEvent({type:"dispose"})}}const go=new Kt,On=new $i,us=new Jn,_o=new P,ds=new P,fs=new P,ps=new P,fr=new P,ms=new P,xo=new P,gs=new P;class Me extends fe{constructor(t=new me,e=new qn){super(),this.isMesh=!0,this.type="Mesh",this.geometry=t,this.material=e,this.morphTargetDictionary=void 0,this.morphTargetInfluences=void 0,this.count=1,this.updateMorphTargets()}copy(t,e){return super.copy(t,e),t.morphTargetInfluences!==void 0&&(this.morphTargetInfluences=t.morphTargetInfluences.slice()),t.morphTargetDictionary!==void 0&&(this.morphTargetDictionary=Object.assign({},t.morphTargetDictionary)),this.material=Array.isArray(t.material)?t.material.slice():t.material,this.geometry=t.geometry,this}updateMorphTargets(){const e=this.geometry.morphAttributes,n=Object.keys(e);if(n.length>0){const i=e[n[0]];if(i!==void 0){this.morphTargetInfluences=[],this.morphTargetDictionary={};for(let r=0,a=i.length;r<a;r++){const o=i[r].name||String(r);this.morphTargetInfluences.push(0),this.morphTargetDictionary[o]=r}}}}getVertexPosition(t,e){const n=this.geometry,i=n.attributes.position,r=n.morphAttributes.position,a=n.morphTargetsRelative;e.fromBufferAttribute(i,t);const o=this.morphTargetInfluences;if(r&&o){ms.set(0,0,0);for(let l=0,c=r.length;l<c;l++){const h=o[l],d=r[l];h!==0&&(fr.fromBufferAttribute(d,t),a?ms.addScaledVector(fr,h):ms.addScaledVector(fr.sub(e),h))}e.add(ms)}return e}raycast(t,e){const n=this.geometry,i=this.material,r=this.matrixWorld;i!==void 0&&(n.boundingSphere===null&&n.computeBoundingSphere(),us.copy(n.boundingSphere),us.applyMatrix4(r),On.copy(t.ray).recast(t.near),!(us.containsPoint(On.origin)===!1&&(On.intersectSphere(us,_o)===null||On.origin.distanceToSquared(_o)>(t.far-t.near)**2))&&(go.copy(r).invert(),On.copy(t.ray).applyMatrix4(go),!(n.boundingBox!==null&&On.intersectsBox(n.boundingBox)===!1)&&this._computeIntersections(t,e,On)))}_computeIntersections(t,e,n){let i;const r=this.geometry,a=this.material,o=r.index,l=r.attributes.position,c=r.attributes.uv,h=r.attributes.uv1,d=r.attributes.normal,u=r.groups,p=r.drawRange;if(o!==null)if(Array.isArray(a))for(let g=0,v=u.length;g<v;g++){const m=u[g],f=a[m.materialIndex],E=Math.max(m.start,p.start),b=Math.min(o.count,Math.min(m.start+m.count,p.start+p.count));for(let S=E,w=b;S<w;S+=3){const A=o.getX(S),C=o.getX(S+1),D=o.getX(S+2);i=_s(this,f,t,n,c,h,d,A,C,D),i&&(i.faceIndex=Math.floor(S/3),i.face.materialIndex=m.materialIndex,e.push(i))}}else{const g=Math.max(0,p.start),v=Math.min(o.count,p.start+p.count);for(let m=g,f=v;m<f;m+=3){const E=o.getX(m),b=o.getX(m+1),S=o.getX(m+2);i=_s(this,a,t,n,c,h,d,E,b,S),i&&(i.faceIndex=Math.floor(m/3),e.push(i))}}else if(l!==void 0)if(Array.isArray(a))for(let g=0,v=u.length;g<v;g++){const m=u[g],f=a[m.materialIndex],E=Math.max(m.start,p.start),b=Math.min(l.count,Math.min(m.start+m.count,p.start+p.count));for(let S=E,w=b;S<w;S+=3){const A=S,C=S+1,D=S+2;i=_s(this,f,t,n,c,h,d,A,C,D),i&&(i.faceIndex=Math.floor(S/3),i.face.materialIndex=m.materialIndex,e.push(i))}}else{const g=Math.max(0,p.start),v=Math.min(l.count,p.start+p.count);for(let m=g,f=v;m<f;m+=3){const E=m,b=m+1,S=m+2;i=_s(this,a,t,n,c,h,d,E,b,S),i&&(i.faceIndex=Math.floor(m/3),e.push(i))}}}}function vh(s,t,e,n,i,r,a,o){let l;if(t.side===Ie?l=n.intersectTriangle(a,r,i,!0,o):l=n.intersectTriangle(i,r,a,t.side===Dn,o),l===null)return null;gs.copy(o),gs.applyMatrix4(s.matrixWorld);const c=e.ray.origin.distanceTo(gs);return c<e.near||c>e.far?null:{distance:c,point:gs.clone(),object:s}}function _s(s,t,e,n,i,r,a,o,l,c){s.getVertexPosition(o,ds),s.getVertexPosition(l,fs),s.getVertexPosition(c,ps);const h=vh(s,t,e,n,ds,fs,ps,xo);if(h){const d=new P;Ke.getBarycoord(xo,ds,fs,ps,d),i&&(h.uv=Ke.getInterpolatedAttribute(i,o,l,c,d,new dt)),r&&(h.uv1=Ke.getInterpolatedAttribute(r,o,l,c,d,new dt)),a&&(h.normal=Ke.getInterpolatedAttribute(a,o,l,c,d,new P),h.normal.dot(n.direction)>0&&h.normal.multiplyScalar(-1));const u={a:o,b:l,c,normal:new P,materialIndex:0};Ke.getNormal(ds,fs,ps,u.normal),h.face=u,h.barycoord=d}return h}class De extends me{constructor(t=1,e=1,n=1,i=1,r=1,a=1){super(),this.type="BoxGeometry",this.parameters={width:t,height:e,depth:n,widthSegments:i,heightSegments:r,depthSegments:a};const o=this;i=Math.floor(i),r=Math.floor(r),a=Math.floor(a);const l=[],c=[],h=[],d=[];let u=0,p=0;g("z","y","x",-1,-1,n,e,t,a,r,0),g("z","y","x",1,-1,n,e,-t,a,r,1),g("x","z","y",1,1,t,n,e,i,a,2),g("x","z","y",1,-1,t,n,-e,i,a,3),g("x","y","z",1,-1,t,e,n,i,r,4),g("x","y","z",-1,-1,t,e,-n,i,r,5),this.setIndex(l),this.setAttribute("position",new Xt(c,3)),this.setAttribute("normal",new Xt(h,3)),this.setAttribute("uv",new Xt(d,2));function g(v,m,f,E,b,S,w,A,C,D,y){const x=S/C,L=w/D,N=S/2,k=w/2,X=A/2,W=C+1,q=D+1;let Z=0,H=0;const rt=new P;for(let ct=0;ct<q;ct++){const Et=ct*L-k;for(let kt=0;kt<W;kt++){const te=kt*x-N;rt[v]=te*E,rt[m]=Et*b,rt[f]=X,c.push(rt.x,rt.y,rt.z),rt[v]=0,rt[m]=0,rt[f]=A>0?1:-1,h.push(rt.x,rt.y,rt.z),d.push(kt/C),d.push(1-ct/D),Z+=1}}for(let ct=0;ct<D;ct++)for(let Et=0;Et<C;Et++){const kt=u+Et+W*ct,te=u+Et+W*(ct+1),se=u+(Et+1)+W*(ct+1),qt=u+(Et+1)+W*ct;l.push(kt,te,qt),l.push(te,se,qt),H+=6}o.addGroup(p,H,y),p+=H,u+=Z}}copy(t){return super.copy(t),this.parameters=Object.assign({},t.parameters),this}static fromJSON(t){return new De(t.width,t.height,t.depth,t.widthSegments,t.heightSegments,t.depthSegments)}}function bi(s){const t={};for(const e in s){t[e]={};for(const n in s[e]){const i=s[e][n];i&&(i.isColor||i.isMatrix3||i.isMatrix4||i.isVector2||i.isVector3||i.isVector4||i.isTexture||i.isQuaternion)?i.isRenderTargetTexture?(console.warn("UniformsUtils: Textures of render targets cannot be cloned via cloneUniforms() or mergeUniforms()."),t[e][n]=null):t[e][n]=i.clone():Array.isArray(i)?t[e][n]=i.slice():t[e][n]=i}}return t}function Ae(s){const t={};for(let e=0;e<s.length;e++){const n=bi(s[e]);for(const i in n)t[i]=n[i]}return t}function yh(s){const t=[];for(let e=0;e<s.length;e++)t.push(s[e].clone());return t}function Bl(s){const t=s.getRenderTarget();return t===null?s.outputColorSpace:t.isXRRenderTarget===!0?t.texture.colorSpace:Gt.workingColorSpace}const Zi={clone:bi,merge:Ae};var Mh=`void main() {
	gl_Position = projectionMatrix * modelViewMatrix * vec4( position, 1.0 );
}`,Sh=`void main() {
	gl_FragColor = vec4( 1.0, 0.0, 0.0, 1.0 );
}`;class Ce extends Qn{constructor(t){super(),this.isShaderMaterial=!0,this.type="ShaderMaterial",this.defines={},this.uniforms={},this.uniformsGroups=[],this.vertexShader=Mh,this.fragmentShader=Sh,this.linewidth=1,this.wireframe=!1,this.wireframeLinewidth=1,this.fog=!1,this.lights=!1,this.clipping=!1,this.forceSinglePass=!0,this.extensions={clipCullDistance:!1,multiDraw:!1},this.defaultAttributeValues={color:[1,1,1],uv:[0,0],uv1:[0,0]},this.index0AttributeName=void 0,this.uniformsNeedUpdate=!1,this.glslVersion=null,t!==void 0&&this.setValues(t)}copy(t){return super.copy(t),this.fragmentShader=t.fragmentShader,this.vertexShader=t.vertexShader,this.uniforms=bi(t.uniforms),this.uniformsGroups=yh(t.uniformsGroups),this.defines=Object.assign({},t.defines),this.wireframe=t.wireframe,this.wireframeLinewidth=t.wireframeLinewidth,this.fog=t.fog,this.lights=t.lights,this.clipping=t.clipping,this.extensions=Object.assign({},t.extensions),this.glslVersion=t.glslVersion,this}toJSON(t){const e=super.toJSON(t);e.glslVersion=this.glslVersion,e.uniforms={};for(const i in this.uniforms){const a=this.uniforms[i].value;a&&a.isTexture?e.uniforms[i]={type:"t",value:a.toJSON(t).uuid}:a&&a.isColor?e.uniforms[i]={type:"c",value:a.getHex()}:a&&a.isVector2?e.uniforms[i]={type:"v2",value:a.toArray()}:a&&a.isVector3?e.uniforms[i]={type:"v3",value:a.toArray()}:a&&a.isVector4?e.uniforms[i]={type:"v4",value:a.toArray()}:a&&a.isMatrix3?e.uniforms[i]={type:"m3",value:a.toArray()}:a&&a.isMatrix4?e.uniforms[i]={type:"m4",value:a.toArray()}:e.uniforms[i]={value:a}}Object.keys(this.defines).length>0&&(e.defines=this.defines),e.vertexShader=this.vertexShader,e.fragmentShader=this.fragmentShader,e.lights=this.lights,e.clipping=this.clipping;const n={};for(const i in this.extensions)this.extensions[i]===!0&&(n[i]=!0);return Object.keys(n).length>0&&(e.extensions=n),e}}class zl extends fe{constructor(){super(),this.isCamera=!0,this.type="Camera",this.matrixWorldInverse=new Kt,this.projectionMatrix=new Kt,this.projectionMatrixInverse=new Kt,this.coordinateSystem=on,this._reversedDepth=!1}get reversedDepth(){return this._reversedDepth}copy(t,e){return super.copy(t,e),this.matrixWorldInverse.copy(t.matrixWorldInverse),this.projectionMatrix.copy(t.projectionMatrix),this.projectionMatrixInverse.copy(t.projectionMatrixInverse),this.coordinateSystem=t.coordinateSystem,this}getWorldDirection(t){return super.getWorldDirection(t).negate()}updateMatrixWorld(t){super.updateMatrixWorld(t),this.matrixWorldInverse.copy(this.matrixWorld).invert()}updateWorldMatrix(t,e){super.updateWorldMatrix(t,e),this.matrixWorldInverse.copy(this.matrixWorld).invert()}clone(){return new this.constructor().copy(this)}}const An=new P,vo=new dt,yo=new dt;class We extends zl{constructor(t=50,e=1,n=.1,i=2e3){super(),this.isPerspectiveCamera=!0,this.type="PerspectiveCamera",this.fov=t,this.zoom=1,this.near=n,this.far=i,this.focus=10,this.aspect=e,this.view=null,this.filmGauge=35,this.filmOffset=0,this.updateProjectionMatrix()}copy(t,e){return super.copy(t,e),this.fov=t.fov,this.zoom=t.zoom,this.near=t.near,this.far=t.far,this.focus=t.focus,this.aspect=t.aspect,this.view=t.view===null?null:Object.assign({},t.view),this.filmGauge=t.filmGauge,this.filmOffset=t.filmOffset,this}setFocalLength(t){const e=.5*this.getFilmHeight()/t;this.fov=ji*2*Math.atan(e),this.updateProjectionMatrix()}getFocalLength(){const t=Math.tan(ki*.5*this.fov);return .5*this.getFilmHeight()/t}getEffectiveFOV(){return ji*2*Math.atan(Math.tan(ki*.5*this.fov)/this.zoom)}getFilmWidth(){return this.filmGauge*Math.min(this.aspect,1)}getFilmHeight(){return this.filmGauge/Math.max(this.aspect,1)}getViewBounds(t,e,n){An.set(-1,-1,.5).applyMatrix4(this.projectionMatrixInverse),e.set(An.x,An.y).multiplyScalar(-t/An.z),An.set(1,1,.5).applyMatrix4(this.projectionMatrixInverse),n.set(An.x,An.y).multiplyScalar(-t/An.z)}getViewSize(t,e){return this.getViewBounds(t,vo,yo),e.subVectors(yo,vo)}setViewOffset(t,e,n,i,r,a){this.aspect=t/e,this.view===null&&(this.view={enabled:!0,fullWidth:1,fullHeight:1,offsetX:0,offsetY:0,width:1,height:1}),this.view.enabled=!0,this.view.fullWidth=t,this.view.fullHeight=e,this.view.offsetX=n,this.view.offsetY=i,this.view.width=r,this.view.height=a,this.updateProjectionMatrix()}clearViewOffset(){this.view!==null&&(this.view.enabled=!1),this.updateProjectionMatrix()}updateProjectionMatrix(){const t=this.near;let e=t*Math.tan(ki*.5*this.fov)/this.zoom,n=2*e,i=this.aspect*n,r=-.5*i;const a=this.view;if(this.view!==null&&this.view.enabled){const l=a.fullWidth,c=a.fullHeight;r+=a.offsetX*i/l,e-=a.offsetY*n/c,i*=a.width/l,n*=a.height/c}const o=this.filmOffset;o!==0&&(r+=t*o/this.getFilmWidth()),this.projectionMatrix.makePerspective(r,r+i,e,e-n,t,this.far,this.coordinateSystem,this.reversedDepth),this.projectionMatrixInverse.copy(this.projectionMatrix).invert()}toJSON(t){const e=super.toJSON(t);return e.object.fov=this.fov,e.object.zoom=this.zoom,e.object.near=this.near,e.object.far=this.far,e.object.focus=this.focus,e.object.aspect=this.aspect,this.view!==null&&(e.object.view=Object.assign({},this.view)),e.object.filmGauge=this.filmGauge,e.object.filmOffset=this.filmOffset,e}}const di=-90,fi=1;class Eh extends fe{constructor(t,e,n){super(),this.type="CubeCamera",this.renderTarget=n,this.coordinateSystem=null,this.activeMipmapLevel=0;const i=new We(di,fi,t,e);i.layers=this.layers,this.add(i);const r=new We(di,fi,t,e);r.layers=this.layers,this.add(r);const a=new We(di,fi,t,e);a.layers=this.layers,this.add(a);const o=new We(di,fi,t,e);o.layers=this.layers,this.add(o);const l=new We(di,fi,t,e);l.layers=this.layers,this.add(l);const c=new We(di,fi,t,e);c.layers=this.layers,this.add(c)}updateCoordinateSystem(){const t=this.coordinateSystem,e=this.children.concat(),[n,i,r,a,o,l]=e;for(const c of e)this.remove(c);if(t===on)n.up.set(0,1,0),n.lookAt(1,0,0),i.up.set(0,1,0),i.lookAt(-1,0,0),r.up.set(0,0,-1),r.lookAt(0,1,0),a.up.set(0,0,1),a.lookAt(0,-1,0),o.up.set(0,1,0),o.lookAt(0,0,1),l.up.set(0,1,0),l.lookAt(0,0,-1);else if(t===Os)n.up.set(0,-1,0),n.lookAt(-1,0,0),i.up.set(0,-1,0),i.lookAt(1,0,0),r.up.set(0,0,1),r.lookAt(0,1,0),a.up.set(0,0,-1),a.lookAt(0,-1,0),o.up.set(0,-1,0),o.lookAt(0,0,1),l.up.set(0,-1,0),l.lookAt(0,0,-1);else throw new Error("THREE.CubeCamera.updateCoordinateSystem(): Invalid coordinate system: "+t);for(const c of e)this.add(c),c.updateMatrixWorld()}update(t,e){this.parent===null&&this.updateMatrixWorld();const{renderTarget:n,activeMipmapLevel:i}=this;this.coordinateSystem!==t.coordinateSystem&&(this.coordinateSystem=t.coordinateSystem,this.updateCoordinateSystem());const[r,a,o,l,c,h]=this.children,d=t.getRenderTarget(),u=t.getActiveCubeFace(),p=t.getActiveMipmapLevel(),g=t.xr.enabled;t.xr.enabled=!1;const v=n.texture.generateMipmaps;n.texture.generateMipmaps=!1,t.setRenderTarget(n,0,i),t.render(e,r),t.setRenderTarget(n,1,i),t.render(e,a),t.setRenderTarget(n,2,i),t.render(e,o),t.setRenderTarget(n,3,i),t.render(e,l),t.setRenderTarget(n,4,i),t.render(e,c),n.texture.generateMipmaps=v,t.setRenderTarget(n,5,i),t.render(e,h),t.setRenderTarget(d,u,p),t.xr.enabled=g,n.texture.needsPMREMUpdate=!0}}class kl extends Re{constructor(t=[],e=Mi,n,i,r,a,o,l,c,h){super(t,e,n,i,r,a,o,l,c,h),this.isCubeTexture=!0,this.flipY=!1}get images(){return this.image}set images(t){this.image=t}}class bh extends $e{constructor(t=1,e={}){super(t,t,e),this.isWebGLCubeRenderTarget=!0;const n={width:t,height:t,depth:1},i=[n,n,n,n,n,n];this.texture=new kl(i),this._setTextureOptions(e),this.texture.isRenderTargetTexture=!0}fromEquirectangularTexture(t,e){this.texture.type=e.type,this.texture.colorSpace=e.colorSpace,this.texture.generateMipmaps=e.generateMipmaps,this.texture.minFilter=e.minFilter,this.texture.magFilter=e.magFilter;const n={uniforms:{tEquirect:{value:null}},vertexShader:`

				varying vec3 vWorldDirection;

				vec3 transformDirection( in vec3 dir, in mat4 matrix ) {

					return normalize( ( matrix * vec4( dir, 0.0 ) ).xyz );

				}

				void main() {

					vWorldDirection = transformDirection( position, modelMatrix );

					#include <begin_vertex>
					#include <project_vertex>

				}
			`,fragmentShader:`

				uniform sampler2D tEquirect;

				varying vec3 vWorldDirection;

				#include <common>

				void main() {

					vec3 direction = normalize( vWorldDirection );

					vec2 sampleUV = equirectUv( direction );

					gl_FragColor = texture2D( tEquirect, sampleUV );

				}
			`},i=new De(5,5,5),r=new Ce({name:"CubemapFromEquirect",uniforms:bi(n.uniforms),vertexShader:n.vertexShader,fragmentShader:n.fragmentShader,side:Ie,blending:vn});r.uniforms.tEquirect.value=e;const a=new Me(i,r),o=e.minFilter;return e.minFilter===Xn&&(e.minFilter=rn),new Eh(1,10,this).update(t,a),e.minFilter=o,a.geometry.dispose(),a.material.dispose(),this}clear(t,e=!0,n=!0,i=!0){const r=t.getRenderTarget();for(let a=0;a<6;a++)t.setRenderTarget(this,a),t.clear(e,n,i);t.setRenderTarget(r)}}class tn extends fe{constructor(){super(),this.isGroup=!0,this.type="Group"}}const Th={type:"move"};class pr{constructor(){this._targetRay=null,this._grip=null,this._hand=null}getHandSpace(){return this._hand===null&&(this._hand=new tn,this._hand.matrixAutoUpdate=!1,this._hand.visible=!1,this._hand.joints={},this._hand.inputState={pinching:!1}),this._hand}getTargetRaySpace(){return this._targetRay===null&&(this._targetRay=new tn,this._targetRay.matrixAutoUpdate=!1,this._targetRay.visible=!1,this._targetRay.hasLinearVelocity=!1,this._targetRay.linearVelocity=new P,this._targetRay.hasAngularVelocity=!1,this._targetRay.angularVelocity=new P),this._targetRay}getGripSpace(){return this._grip===null&&(this._grip=new tn,this._grip.matrixAutoUpdate=!1,this._grip.visible=!1,this._grip.hasLinearVelocity=!1,this._grip.linearVelocity=new P,this._grip.hasAngularVelocity=!1,this._grip.angularVelocity=new P),this._grip}dispatchEvent(t){return this._targetRay!==null&&this._targetRay.dispatchEvent(t),this._grip!==null&&this._grip.dispatchEvent(t),this._hand!==null&&this._hand.dispatchEvent(t),this}connect(t){if(t&&t.hand){const e=this._hand;if(e)for(const n of t.hand.values())this._getHandJoint(e,n)}return this.dispatchEvent({type:"connected",data:t}),this}disconnect(t){return this.dispatchEvent({type:"disconnected",data:t}),this._targetRay!==null&&(this._targetRay.visible=!1),this._grip!==null&&(this._grip.visible=!1),this._hand!==null&&(this._hand.visible=!1),this}update(t,e,n){let i=null,r=null,a=null;const o=this._targetRay,l=this._grip,c=this._hand;if(t&&e.session.visibilityState!=="visible-blurred"){if(c&&t.hand){a=!0;for(const v of t.hand.values()){const m=e.getJointPose(v,n),f=this._getHandJoint(c,v);m!==null&&(f.matrix.fromArray(m.transform.matrix),f.matrix.decompose(f.position,f.rotation,f.scale),f.matrixWorldNeedsUpdate=!0,f.jointRadius=m.radius),f.visible=m!==null}const h=c.joints["index-finger-tip"],d=c.joints["thumb-tip"],u=h.position.distanceTo(d.position),p=.02,g=.005;c.inputState.pinching&&u>p+g?(c.inputState.pinching=!1,this.dispatchEvent({type:"pinchend",handedness:t.handedness,target:this})):!c.inputState.pinching&&u<=p-g&&(c.inputState.pinching=!0,this.dispatchEvent({type:"pinchstart",handedness:t.handedness,target:this}))}else l!==null&&t.gripSpace&&(r=e.getPose(t.gripSpace,n),r!==null&&(l.matrix.fromArray(r.transform.matrix),l.matrix.decompose(l.position,l.rotation,l.scale),l.matrixWorldNeedsUpdate=!0,r.linearVelocity?(l.hasLinearVelocity=!0,l.linearVelocity.copy(r.linearVelocity)):l.hasLinearVelocity=!1,r.angularVelocity?(l.hasAngularVelocity=!0,l.angularVelocity.copy(r.angularVelocity)):l.hasAngularVelocity=!1));o!==null&&(i=e.getPose(t.targetRaySpace,n),i===null&&r!==null&&(i=r),i!==null&&(o.matrix.fromArray(i.transform.matrix),o.matrix.decompose(o.position,o.rotation,o.scale),o.matrixWorldNeedsUpdate=!0,i.linearVelocity?(o.hasLinearVelocity=!0,o.linearVelocity.copy(i.linearVelocity)):o.hasLinearVelocity=!1,i.angularVelocity?(o.hasAngularVelocity=!0,o.angularVelocity.copy(i.angularVelocity)):o.hasAngularVelocity=!1,this.dispatchEvent(Th)))}return o!==null&&(o.visible=i!==null),l!==null&&(l.visible=r!==null),c!==null&&(c.visible=a!==null),this}_getHandJoint(t,e){if(t.joints[e.jointName]===void 0){const n=new tn;n.matrixAutoUpdate=!1,n.visible=!1,t.joints[e.jointName]=n,t.add(n)}return t.joints[e.jointName]}}class Ia{constructor(t,e=25e-5){this.isFogExp2=!0,this.name="",this.color=new Ft(t),this.density=e}clone(){return new Ia(this.color,this.density)}toJSON(){return{type:"FogExp2",name:this.name,color:this.color.getHex(),density:this.density}}}class wh extends fe{constructor(){super(),this.isScene=!0,this.type="Scene",this.background=null,this.environment=null,this.fog=null,this.backgroundBlurriness=0,this.backgroundIntensity=1,this.backgroundRotation=new cn,this.environmentIntensity=1,this.environmentRotation=new cn,this.overrideMaterial=null,typeof __THREE_DEVTOOLS__<"u"&&__THREE_DEVTOOLS__.dispatchEvent(new CustomEvent("observe",{detail:this}))}copy(t,e){return super.copy(t,e),t.background!==null&&(this.background=t.background.clone()),t.environment!==null&&(this.environment=t.environment.clone()),t.fog!==null&&(this.fog=t.fog.clone()),this.backgroundBlurriness=t.backgroundBlurriness,this.backgroundIntensity=t.backgroundIntensity,this.backgroundRotation.copy(t.backgroundRotation),this.environmentIntensity=t.environmentIntensity,this.environmentRotation.copy(t.environmentRotation),t.overrideMaterial!==null&&(this.overrideMaterial=t.overrideMaterial.clone()),this.matrixAutoUpdate=t.matrixAutoUpdate,this}toJSON(t){const e=super.toJSON(t);return this.fog!==null&&(e.object.fog=this.fog.toJSON()),this.backgroundBlurriness>0&&(e.object.backgroundBlurriness=this.backgroundBlurriness),this.backgroundIntensity!==1&&(e.object.backgroundIntensity=this.backgroundIntensity),e.object.backgroundRotation=this.backgroundRotation.toArray(),this.environmentIntensity!==1&&(e.object.environmentIntensity=this.environmentIntensity),e.object.environmentRotation=this.environmentRotation.toArray(),e}}class Ah extends Re{constructor(t=null,e=1,n=1,i,r,a,o,l,c=ze,h=ze,d,u){super(null,a,o,l,c,h,i,r,d,u),this.isDataTexture=!0,this.image={data:t,width:e,height:n},this.generateMipmaps=!1,this.flipY=!1,this.unpackAlignment=1}}class Mo extends Xe{constructor(t,e,n,i=1){super(t,e,n),this.isInstancedBufferAttribute=!0,this.meshPerAttribute=i}copy(t){return super.copy(t),this.meshPerAttribute=t.meshPerAttribute,this}toJSON(){const t=super.toJSON();return t.meshPerAttribute=this.meshPerAttribute,t.isInstancedBufferAttribute=!0,t}}const pi=new Kt,So=new Kt,xs=[],Eo=new $n,Ch=new Kt,Ui=new Me,Ni=new Jn;class Rh extends Me{constructor(t,e,n){super(t,e),this.isInstancedMesh=!0,this.instanceMatrix=new Mo(new Float32Array(n*16),16),this.instanceColor=null,this.morphTexture=null,this.count=n,this.boundingBox=null,this.boundingSphere=null;for(let i=0;i<n;i++)this.setMatrixAt(i,Ch)}computeBoundingBox(){const t=this.geometry,e=this.count;this.boundingBox===null&&(this.boundingBox=new $n),t.boundingBox===null&&t.computeBoundingBox(),this.boundingBox.makeEmpty();for(let n=0;n<e;n++)this.getMatrixAt(n,pi),Eo.copy(t.boundingBox).applyMatrix4(pi),this.boundingBox.union(Eo)}computeBoundingSphere(){const t=this.geometry,e=this.count;this.boundingSphere===null&&(this.boundingSphere=new Jn),t.boundingSphere===null&&t.computeBoundingSphere(),this.boundingSphere.makeEmpty();for(let n=0;n<e;n++)this.getMatrixAt(n,pi),Ni.copy(t.boundingSphere).applyMatrix4(pi),this.boundingSphere.union(Ni)}copy(t,e){return super.copy(t,e),this.instanceMatrix.copy(t.instanceMatrix),t.morphTexture!==null&&(this.morphTexture=t.morphTexture.clone()),t.instanceColor!==null&&(this.instanceColor=t.instanceColor.clone()),this.count=t.count,t.boundingBox!==null&&(this.boundingBox=t.boundingBox.clone()),t.boundingSphere!==null&&(this.boundingSphere=t.boundingSphere.clone()),this}getColorAt(t,e){e.fromArray(this.instanceColor.array,t*3)}getMatrixAt(t,e){e.fromArray(this.instanceMatrix.array,t*16)}getMorphAt(t,e){const n=e.morphTargetInfluences,i=this.morphTexture.source.data.data,r=n.length+1,a=t*r+1;for(let o=0;o<n.length;o++)n[o]=i[a+o]}raycast(t,e){const n=this.matrixWorld,i=this.count;if(Ui.geometry=this.geometry,Ui.material=this.material,Ui.material!==void 0&&(this.boundingSphere===null&&this.computeBoundingSphere(),Ni.copy(this.boundingSphere),Ni.applyMatrix4(n),t.ray.intersectsSphere(Ni)!==!1))for(let r=0;r<i;r++){this.getMatrixAt(r,pi),So.multiplyMatrices(n,pi),Ui.matrixWorld=So,Ui.raycast(t,xs);for(let a=0,o=xs.length;a<o;a++){const l=xs[a];l.instanceId=r,l.object=this,e.push(l)}xs.length=0}}setColorAt(t,e){this.instanceColor===null&&(this.instanceColor=new Mo(new Float32Array(this.instanceMatrix.count*3).fill(1),3)),e.toArray(this.instanceColor.array,t*3)}setMatrixAt(t,e){e.toArray(this.instanceMatrix.array,t*16)}setMorphAt(t,e){const n=e.morphTargetInfluences,i=n.length+1;this.morphTexture===null&&(this.morphTexture=new Ah(new Float32Array(i*this.count),i,this.count,wa,an));const r=this.morphTexture.source.data.data;let a=0;for(let c=0;c<n.length;c++)a+=n[c];const o=this.geometry.morphTargetsRelative?1:1-a,l=i*t;r[l]=o,r.set(n,l+1)}updateMorphTargets(){}dispose(){this.dispatchEvent({type:"dispose"}),this.morphTexture!==null&&(this.morphTexture.dispose(),this.morphTexture=null)}}const mr=new P,Ph=new P,Lh=new Nt;class Cn{constructor(t=new P(1,0,0),e=0){this.isPlane=!0,this.normal=t,this.constant=e}set(t,e){return this.normal.copy(t),this.constant=e,this}setComponents(t,e,n,i){return this.normal.set(t,e,n),this.constant=i,this}setFromNormalAndCoplanarPoint(t,e){return this.normal.copy(t),this.constant=-e.dot(this.normal),this}setFromCoplanarPoints(t,e,n){const i=mr.subVectors(n,e).cross(Ph.subVectors(t,e)).normalize();return this.setFromNormalAndCoplanarPoint(i,t),this}copy(t){return this.normal.copy(t.normal),this.constant=t.constant,this}normalize(){const t=1/this.normal.length();return this.normal.multiplyScalar(t),this.constant*=t,this}negate(){return this.constant*=-1,this.normal.negate(),this}distanceToPoint(t){return this.normal.dot(t)+this.constant}distanceToSphere(t){return this.distanceToPoint(t.center)-t.radius}projectPoint(t,e){return e.copy(t).addScaledVector(this.normal,-this.distanceToPoint(t))}intersectLine(t,e){const n=t.delta(mr),i=this.normal.dot(n);if(i===0)return this.distanceToPoint(t.start)===0?e.copy(t.start):null;const r=-(t.start.dot(this.normal)+this.constant)/i;return r<0||r>1?null:e.copy(t.start).addScaledVector(n,r)}intersectsLine(t){const e=this.distanceToPoint(t.start),n=this.distanceToPoint(t.end);return e<0&&n>0||n<0&&e>0}intersectsBox(t){return t.intersectsPlane(this)}intersectsSphere(t){return t.intersectsPlane(this)}coplanarPoint(t){return t.copy(this.normal).multiplyScalar(-this.constant)}applyMatrix4(t,e){const n=e||Lh.getNormalMatrix(t),i=this.coplanarPoint(mr).applyMatrix4(t),r=this.normal.applyMatrix3(n).normalize();return this.constant=-i.dot(r),this}translate(t){return this.constant-=t.dot(this.normal),this}equals(t){return t.normal.equals(this.normal)&&t.constant===this.constant}clone(){return new this.constructor().copy(this)}}const Bn=new Jn,Dh=new dt(.5,.5),vs=new P;class Ua{constructor(t=new Cn,e=new Cn,n=new Cn,i=new Cn,r=new Cn,a=new Cn){this.planes=[t,e,n,i,r,a]}set(t,e,n,i,r,a){const o=this.planes;return o[0].copy(t),o[1].copy(e),o[2].copy(n),o[3].copy(i),o[4].copy(r),o[5].copy(a),this}copy(t){const e=this.planes;for(let n=0;n<6;n++)e[n].copy(t.planes[n]);return this}setFromProjectionMatrix(t,e=on,n=!1){const i=this.planes,r=t.elements,a=r[0],o=r[1],l=r[2],c=r[3],h=r[4],d=r[5],u=r[6],p=r[7],g=r[8],v=r[9],m=r[10],f=r[11],E=r[12],b=r[13],S=r[14],w=r[15];if(i[0].setComponents(c-a,p-h,f-g,w-E).normalize(),i[1].setComponents(c+a,p+h,f+g,w+E).normalize(),i[2].setComponents(c+o,p+d,f+v,w+b).normalize(),i[3].setComponents(c-o,p-d,f-v,w-b).normalize(),n)i[4].setComponents(l,u,m,S).normalize(),i[5].setComponents(c-l,p-u,f-m,w-S).normalize();else if(i[4].setComponents(c-l,p-u,f-m,w-S).normalize(),e===on)i[5].setComponents(c+l,p+u,f+m,w+S).normalize();else if(e===Os)i[5].setComponents(l,u,m,S).normalize();else throw new Error("THREE.Frustum.setFromProjectionMatrix(): Invalid coordinate system: "+e);return this}intersectsObject(t){if(t.boundingSphere!==void 0)t.boundingSphere===null&&t.computeBoundingSphere(),Bn.copy(t.boundingSphere).applyMatrix4(t.matrixWorld);else{const e=t.geometry;e.boundingSphere===null&&e.computeBoundingSphere(),Bn.copy(e.boundingSphere).applyMatrix4(t.matrixWorld)}return this.intersectsSphere(Bn)}intersectsSprite(t){Bn.center.set(0,0,0);const e=Dh.distanceTo(t.center);return Bn.radius=.7071067811865476+e,Bn.applyMatrix4(t.matrixWorld),this.intersectsSphere(Bn)}intersectsSphere(t){const e=this.planes,n=t.center,i=-t.radius;for(let r=0;r<6;r++)if(e[r].distanceToPoint(n)<i)return!1;return!0}intersectsBox(t){const e=this.planes;for(let n=0;n<6;n++){const i=e[n];if(vs.x=i.normal.x>0?t.max.x:t.min.x,vs.y=i.normal.y>0?t.max.y:t.min.y,vs.z=i.normal.z>0?t.max.z:t.min.z,i.distanceToPoint(vs)<0)return!1}return!0}containsPoint(t){const e=this.planes;for(let n=0;n<6;n++)if(e[n].distanceToPoint(t)<0)return!1;return!0}clone(){return new this.constructor().copy(this)}}class Hl extends Qn{constructor(t){super(),this.isLineBasicMaterial=!0,this.type="LineBasicMaterial",this.color=new Ft(16777215),this.map=null,this.linewidth=1,this.linecap="round",this.linejoin="round",this.fog=!0,this.setValues(t)}copy(t){return super.copy(t),this.color.copy(t.color),this.map=t.map,this.linewidth=t.linewidth,this.linecap=t.linecap,this.linejoin=t.linejoin,this.fog=t.fog,this}}const zs=new P,ks=new P,bo=new Kt,Fi=new $i,ys=new Jn,gr=new P,To=new P;class Ih extends fe{constructor(t=new me,e=new Hl){super(),this.isLine=!0,this.type="Line",this.geometry=t,this.material=e,this.morphTargetDictionary=void 0,this.morphTargetInfluences=void 0,this.updateMorphTargets()}copy(t,e){return super.copy(t,e),this.material=Array.isArray(t.material)?t.material.slice():t.material,this.geometry=t.geometry,this}computeLineDistances(){const t=this.geometry;if(t.index===null){const e=t.attributes.position,n=[0];for(let i=1,r=e.count;i<r;i++)zs.fromBufferAttribute(e,i-1),ks.fromBufferAttribute(e,i),n[i]=n[i-1],n[i]+=zs.distanceTo(ks);t.setAttribute("lineDistance",new Xt(n,1))}else console.warn("THREE.Line.computeLineDistances(): Computation only possible with non-indexed BufferGeometry.");return this}raycast(t,e){const n=this.geometry,i=this.matrixWorld,r=t.params.Line.threshold,a=n.drawRange;if(n.boundingSphere===null&&n.computeBoundingSphere(),ys.copy(n.boundingSphere),ys.applyMatrix4(i),ys.radius+=r,t.ray.intersectsSphere(ys)===!1)return;bo.copy(i).invert(),Fi.copy(t.ray).applyMatrix4(bo);const o=r/((this.scale.x+this.scale.y+this.scale.z)/3),l=o*o,c=this.isLineSegments?2:1,h=n.index,u=n.attributes.position;if(h!==null){const p=Math.max(0,a.start),g=Math.min(h.count,a.start+a.count);for(let v=p,m=g-1;v<m;v+=c){const f=h.getX(v),E=h.getX(v+1),b=Ms(this,t,Fi,l,f,E,v);b&&e.push(b)}if(this.isLineLoop){const v=h.getX(g-1),m=h.getX(p),f=Ms(this,t,Fi,l,v,m,g-1);f&&e.push(f)}}else{const p=Math.max(0,a.start),g=Math.min(u.count,a.start+a.count);for(let v=p,m=g-1;v<m;v+=c){const f=Ms(this,t,Fi,l,v,v+1,v);f&&e.push(f)}if(this.isLineLoop){const v=Ms(this,t,Fi,l,g-1,p,g-1);v&&e.push(v)}}}updateMorphTargets(){const e=this.geometry.morphAttributes,n=Object.keys(e);if(n.length>0){const i=e[n[0]];if(i!==void 0){this.morphTargetInfluences=[],this.morphTargetDictionary={};for(let r=0,a=i.length;r<a;r++){const o=i[r].name||String(r);this.morphTargetInfluences.push(0),this.morphTargetDictionary[o]=r}}}}}function Ms(s,t,e,n,i,r,a){const o=s.geometry.attributes.position;if(zs.fromBufferAttribute(o,i),ks.fromBufferAttribute(o,r),e.distanceSqToSegment(zs,ks,gr,To)>n)return;gr.applyMatrix4(s.matrixWorld);const c=t.ray.origin.distanceTo(gr);if(!(c<t.near||c>t.far))return{distance:c,point:To.clone().applyMatrix4(s.matrixWorld),index:a,face:null,faceIndex:null,barycoord:null,object:s}}class Vl extends Qn{constructor(t){super(),this.isPointsMaterial=!0,this.type="PointsMaterial",this.color=new Ft(16777215),this.map=null,this.alphaMap=null,this.size=1,this.sizeAttenuation=!0,this.fog=!0,this.setValues(t)}copy(t){return super.copy(t),this.color.copy(t.color),this.map=t.map,this.alphaMap=t.alphaMap,this.size=t.size,this.sizeAttenuation=t.sizeAttenuation,this.fog=t.fog,this}}const wo=new Kt,xa=new $i,Ss=new Jn,Es=new P;class Uh extends fe{constructor(t=new me,e=new Vl){super(),this.isPoints=!0,this.type="Points",this.geometry=t,this.material=e,this.morphTargetDictionary=void 0,this.morphTargetInfluences=void 0,this.updateMorphTargets()}copy(t,e){return super.copy(t,e),this.material=Array.isArray(t.material)?t.material.slice():t.material,this.geometry=t.geometry,this}raycast(t,e){const n=this.geometry,i=this.matrixWorld,r=t.params.Points.threshold,a=n.drawRange;if(n.boundingSphere===null&&n.computeBoundingSphere(),Ss.copy(n.boundingSphere),Ss.applyMatrix4(i),Ss.radius+=r,t.ray.intersectsSphere(Ss)===!1)return;wo.copy(i).invert(),xa.copy(t.ray).applyMatrix4(wo);const o=r/((this.scale.x+this.scale.y+this.scale.z)/3),l=o*o,c=n.index,d=n.attributes.position;if(c!==null){const u=Math.max(0,a.start),p=Math.min(c.count,a.start+a.count);for(let g=u,v=p;g<v;g++){const m=c.getX(g);Es.fromBufferAttribute(d,m),Ao(Es,m,l,i,t,e,this)}}else{const u=Math.max(0,a.start),p=Math.min(d.count,a.start+a.count);for(let g=u,v=p;g<v;g++)Es.fromBufferAttribute(d,g),Ao(Es,g,l,i,t,e,this)}}updateMorphTargets(){const e=this.geometry.morphAttributes,n=Object.keys(e);if(n.length>0){const i=e[n[0]];if(i!==void 0){this.morphTargetInfluences=[],this.morphTargetDictionary={};for(let r=0,a=i.length;r<a;r++){const o=i[r].name||String(r);this.morphTargetInfluences.push(0),this.morphTargetDictionary[o]=r}}}}}function Ao(s,t,e,n,i,r,a){const o=xa.distanceSqToPoint(s);if(o<e){const l=new P;xa.closestPointToPoint(s,l),l.applyMatrix4(n);const c=i.ray.origin.distanceTo(l);if(c<i.near||c>i.far)return;r.push({distance:c,distanceToRay:Math.sqrt(o),point:l,index:t,face:null,faceIndex:null,barycoord:null,object:a})}}class Gl extends Re{constructor(t,e,n=jn,i,r,a,o=ze,l=ze,c,h=qi,d=1){if(h!==qi&&h!==Yi)throw new Error("DepthTexture format must be either THREE.DepthFormat or THREE.DepthStencilFormat");const u={width:t,height:e,depth:d};super(u,i,r,a,o,l,h,n,c),this.isDepthTexture=!0,this.flipY=!1,this.generateMipmaps=!1,this.compareFunction=null}copy(t){return super.copy(t),this.source=new La(Object.assign({},t.image)),this.compareFunction=t.compareFunction,this}toJSON(t){const e=super.toJSON(t);return this.compareFunction!==null&&(e.compareFunction=this.compareFunction),e}}class Wl extends Re{constructor(t=null){super(),this.sourceTexture=t,this.isExternalTexture=!0}copy(t){return super.copy(t),this.sourceTexture=t.sourceTexture,this}}class Na extends me{constructor(t=1,e=1,n=4,i=8,r=1){super(),this.type="CapsuleGeometry",this.parameters={radius:t,height:e,capSegments:n,radialSegments:i,heightSegments:r},e=Math.max(0,e),n=Math.max(1,Math.floor(n)),i=Math.max(3,Math.floor(i)),r=Math.max(1,Math.floor(r));const a=[],o=[],l=[],c=[],h=e/2,d=Math.PI/2*t,u=e,p=2*d+u,g=n*2+r,v=i+1,m=new P,f=new P;for(let E=0;E<=g;E++){let b=0,S=0,w=0,A=0;if(E<=n){const y=E/n,x=y*Math.PI/2;S=-h-t*Math.cos(x),w=t*Math.sin(x),A=-t*Math.cos(x),b=y*d}else if(E<=n+r){const y=(E-n)/r;S=-h+y*e,w=t,A=0,b=d+y*u}else{const y=(E-n-r)/n,x=y*Math.PI/2;S=h+t*Math.sin(x),w=t*Math.cos(x),A=t*Math.sin(x),b=d+u+y*d}const C=Math.max(0,Math.min(1,b/p));let D=0;E===0?D=.5/i:E===g&&(D=-.5/i);for(let y=0;y<=i;y++){const x=y/i,L=x*Math.PI*2,N=Math.sin(L),k=Math.cos(L);f.x=-w*k,f.y=S,f.z=w*N,o.push(f.x,f.y,f.z),m.set(-w*k,A,w*N),m.normalize(),l.push(m.x,m.y,m.z),c.push(x+D,C)}if(E>0){const y=(E-1)*v;for(let x=0;x<i;x++){const L=y+x,N=y+x+1,k=E*v+x,X=E*v+x+1;a.push(L,N,k),a.push(N,X,k)}}}this.setIndex(a),this.setAttribute("position",new Xt(o,3)),this.setAttribute("normal",new Xt(l,3)),this.setAttribute("uv",new Xt(c,2))}copy(t){return super.copy(t),this.parameters=Object.assign({},t.parameters),this}static fromJSON(t){return new Na(t.radius,t.height,t.capSegments,t.radialSegments,t.heightSegments)}}class Fa extends me{constructor(t=1,e=32,n=0,i=Math.PI*2){super(),this.type="CircleGeometry",this.parameters={radius:t,segments:e,thetaStart:n,thetaLength:i},e=Math.max(3,e);const r=[],a=[],o=[],l=[],c=new P,h=new dt;a.push(0,0,0),o.push(0,0,1),l.push(.5,.5);for(let d=0,u=3;d<=e;d++,u+=3){const p=n+d/e*i;c.x=t*Math.cos(p),c.y=t*Math.sin(p),a.push(c.x,c.y,c.z),o.push(0,0,1),h.x=(a[u]/t+1)/2,h.y=(a[u+1]/t+1)/2,l.push(h.x,h.y)}for(let d=1;d<=e;d++)r.push(d,d+1,0);this.setIndex(r),this.setAttribute("position",new Xt(a,3)),this.setAttribute("normal",new Xt(o,3)),this.setAttribute("uv",new Xt(l,2))}copy(t){return super.copy(t),this.parameters=Object.assign({},t.parameters),this}static fromJSON(t){return new Fa(t.radius,t.segments,t.thetaStart,t.thetaLength)}}class Ge extends me{constructor(t=1,e=1,n=1,i=32,r=1,a=!1,o=0,l=Math.PI*2){super(),this.type="CylinderGeometry",this.parameters={radiusTop:t,radiusBottom:e,height:n,radialSegments:i,heightSegments:r,openEnded:a,thetaStart:o,thetaLength:l};const c=this;i=Math.floor(i),r=Math.floor(r);const h=[],d=[],u=[],p=[];let g=0;const v=[],m=n/2;let f=0;E(),a===!1&&(t>0&&b(!0),e>0&&b(!1)),this.setIndex(h),this.setAttribute("position",new Xt(d,3)),this.setAttribute("normal",new Xt(u,3)),this.setAttribute("uv",new Xt(p,2));function E(){const S=new P,w=new P;let A=0;const C=(e-t)/n;for(let D=0;D<=r;D++){const y=[],x=D/r,L=x*(e-t)+t;for(let N=0;N<=i;N++){const k=N/i,X=k*l+o,W=Math.sin(X),q=Math.cos(X);w.x=L*W,w.y=-x*n+m,w.z=L*q,d.push(w.x,w.y,w.z),S.set(W,C,q).normalize(),u.push(S.x,S.y,S.z),p.push(k,1-x),y.push(g++)}v.push(y)}for(let D=0;D<i;D++)for(let y=0;y<r;y++){const x=v[y][D],L=v[y+1][D],N=v[y+1][D+1],k=v[y][D+1];(t>0||y!==0)&&(h.push(x,L,k),A+=3),(e>0||y!==r-1)&&(h.push(L,N,k),A+=3)}c.addGroup(f,A,0),f+=A}function b(S){const w=g,A=new dt,C=new P;let D=0;const y=S===!0?t:e,x=S===!0?1:-1;for(let N=1;N<=i;N++)d.push(0,m*x,0),u.push(0,x,0),p.push(.5,.5),g++;const L=g;for(let N=0;N<=i;N++){const X=N/i*l+o,W=Math.cos(X),q=Math.sin(X);C.x=y*q,C.y=m*x,C.z=y*W,d.push(C.x,C.y,C.z),u.push(0,x,0),A.x=W*.5+.5,A.y=q*.5*x+.5,p.push(A.x,A.y),g++}for(let N=0;N<i;N++){const k=w+N,X=L+N;S===!0?h.push(X,X+1,k):h.push(X+1,X,k),D+=3}c.addGroup(f,D,S===!0?1:2),f+=D}}copy(t){return super.copy(t),this.parameters=Object.assign({},t.parameters),this}static fromJSON(t){return new Ge(t.radiusTop,t.radiusBottom,t.height,t.radialSegments,t.heightSegments,t.openEnded,t.thetaStart,t.thetaLength)}}class Hs extends Ge{constructor(t=1,e=1,n=32,i=1,r=!1,a=0,o=Math.PI*2){super(0,t,e,n,i,r,a,o),this.type="ConeGeometry",this.parameters={radius:t,height:e,radialSegments:n,heightSegments:i,openEnded:r,thetaStart:a,thetaLength:o}}static fromJSON(t){return new Hs(t.radius,t.height,t.radialSegments,t.heightSegments,t.openEnded,t.thetaStart,t.thetaLength)}}class Oa extends me{constructor(t=[],e=[],n=1,i=0){super(),this.type="PolyhedronGeometry",this.parameters={vertices:t,indices:e,radius:n,detail:i};const r=[],a=[];o(i),c(n),h(),this.setAttribute("position",new Xt(r,3)),this.setAttribute("normal",new Xt(r.slice(),3)),this.setAttribute("uv",new Xt(a,2)),i===0?this.computeVertexNormals():this.normalizeNormals();function o(E){const b=new P,S=new P,w=new P;for(let A=0;A<e.length;A+=3)p(e[A+0],b),p(e[A+1],S),p(e[A+2],w),l(b,S,w,E)}function l(E,b,S,w){const A=w+1,C=[];for(let D=0;D<=A;D++){C[D]=[];const y=E.clone().lerp(S,D/A),x=b.clone().lerp(S,D/A),L=A-D;for(let N=0;N<=L;N++)N===0&&D===A?C[D][N]=y:C[D][N]=y.clone().lerp(x,N/L)}for(let D=0;D<A;D++)for(let y=0;y<2*(A-D)-1;y++){const x=Math.floor(y/2);y%2===0?(u(C[D][x+1]),u(C[D+1][x]),u(C[D][x])):(u(C[D][x+1]),u(C[D+1][x+1]),u(C[D+1][x]))}}function c(E){const b=new P;for(let S=0;S<r.length;S+=3)b.x=r[S+0],b.y=r[S+1],b.z=r[S+2],b.normalize().multiplyScalar(E),r[S+0]=b.x,r[S+1]=b.y,r[S+2]=b.z}function h(){const E=new P;for(let b=0;b<r.length;b+=3){E.x=r[b+0],E.y=r[b+1],E.z=r[b+2];const S=m(E)/2/Math.PI+.5,w=f(E)/Math.PI+.5;a.push(S,1-w)}g(),d()}function d(){for(let E=0;E<a.length;E+=6){const b=a[E+0],S=a[E+2],w=a[E+4],A=Math.max(b,S,w),C=Math.min(b,S,w);A>.9&&C<.1&&(b<.2&&(a[E+0]+=1),S<.2&&(a[E+2]+=1),w<.2&&(a[E+4]+=1))}}function u(E){r.push(E.x,E.y,E.z)}function p(E,b){const S=E*3;b.x=t[S+0],b.y=t[S+1],b.z=t[S+2]}function g(){const E=new P,b=new P,S=new P,w=new P,A=new dt,C=new dt,D=new dt;for(let y=0,x=0;y<r.length;y+=9,x+=6){E.set(r[y+0],r[y+1],r[y+2]),b.set(r[y+3],r[y+4],r[y+5]),S.set(r[y+6],r[y+7],r[y+8]),A.set(a[x+0],a[x+1]),C.set(a[x+2],a[x+3]),D.set(a[x+4],a[x+5]),w.copy(E).add(b).add(S).divideScalar(3);const L=m(w);v(A,x+0,E,L),v(C,x+2,b,L),v(D,x+4,S,L)}}function v(E,b,S,w){w<0&&E.x===1&&(a[b]=E.x-1),S.x===0&&S.z===0&&(a[b]=w/2/Math.PI+.5)}function m(E){return Math.atan2(E.z,-E.x)}function f(E){return Math.atan2(-E.y,Math.sqrt(E.x*E.x+E.z*E.z))}}copy(t){return super.copy(t),this.parameters=Object.assign({},t.parameters),this}static fromJSON(t){return new Oa(t.vertices,t.indices,t.radius,t.details)}}class Nh{constructor(){this.type="Curve",this.arcLengthDivisions=200,this.needsUpdate=!1,this.cacheArcLengths=null}getPoint(){console.warn("THREE.Curve: .getPoint() not implemented.")}getPointAt(t,e){const n=this.getUtoTmapping(t);return this.getPoint(n,e)}getPoints(t=5){const e=[];for(let n=0;n<=t;n++)e.push(this.getPoint(n/t));return e}getSpacedPoints(t=5){const e=[];for(let n=0;n<=t;n++)e.push(this.getPointAt(n/t));return e}getLength(){const t=this.getLengths();return t[t.length-1]}getLengths(t=this.arcLengthDivisions){if(this.cacheArcLengths&&this.cacheArcLengths.length===t+1&&!this.needsUpdate)return this.cacheArcLengths;this.needsUpdate=!1;const e=[];let n,i=this.getPoint(0),r=0;e.push(0);for(let a=1;a<=t;a++)n=this.getPoint(a/t),r+=n.distanceTo(i),e.push(r),i=n;return this.cacheArcLengths=e,e}updateArcLengths(){this.needsUpdate=!0,this.getLengths()}getUtoTmapping(t,e=null){const n=this.getLengths();let i=0;const r=n.length;let a;e?a=e:a=t*n[r-1];let o=0,l=r-1,c;for(;o<=l;)if(i=Math.floor(o+(l-o)/2),c=n[i]-a,c<0)o=i+1;else if(c>0)l=i-1;else{l=i;break}if(i=l,n[i]===a)return i/(r-1);const h=n[i],u=n[i+1]-h,p=(a-h)/u;return(i+p)/(r-1)}getTangent(t,e){let i=t-1e-4,r=t+1e-4;i<0&&(i=0),r>1&&(r=1);const a=this.getPoint(i),o=this.getPoint(r),l=e||(a.isVector2?new dt:new P);return l.copy(o).sub(a).normalize(),l}getTangentAt(t,e){const n=this.getUtoTmapping(t);return this.getTangent(n,e)}computeFrenetFrames(t,e=!1){const n=new P,i=[],r=[],a=[],o=new P,l=new Kt;for(let p=0;p<=t;p++){const g=p/t;i[p]=this.getTangentAt(g,new P)}r[0]=new P,a[0]=new P;let c=Number.MAX_VALUE;const h=Math.abs(i[0].x),d=Math.abs(i[0].y),u=Math.abs(i[0].z);h<=c&&(c=h,n.set(1,0,0)),d<=c&&(c=d,n.set(0,1,0)),u<=c&&n.set(0,0,1),o.crossVectors(i[0],n).normalize(),r[0].crossVectors(i[0],o),a[0].crossVectors(i[0],r[0]);for(let p=1;p<=t;p++){if(r[p]=r[p-1].clone(),a[p]=a[p-1].clone(),o.crossVectors(i[p-1],i[p]),o.length()>Number.EPSILON){o.normalize();const g=Math.acos(zt(i[p-1].dot(i[p]),-1,1));r[p].applyMatrix4(l.makeRotationAxis(o,g))}a[p].crossVectors(i[p],r[p])}if(e===!0){let p=Math.acos(zt(r[0].dot(r[t]),-1,1));p/=t,i[0].dot(o.crossVectors(r[0],r[t]))>0&&(p=-p);for(let g=1;g<=t;g++)r[g].applyMatrix4(l.makeRotationAxis(i[g],p*g)),a[g].crossVectors(i[g],r[g])}return{tangents:i,normals:r,binormals:a}}clone(){return new this.constructor().copy(this)}copy(t){return this.arcLengthDivisions=t.arcLengthDivisions,this}toJSON(){const t={metadata:{version:4.7,type:"Curve",generator:"Curve.toJSON"}};return t.arcLengthDivisions=this.arcLengthDivisions,t.type=this.type,t}fromJSON(t){return this.arcLengthDivisions=t.arcLengthDivisions,this}}function Fh(s,t){const e=1-s;return e*e*t}function Oh(s,t){return 2*(1-s)*s*t}function Bh(s,t){return s*s*t}function _r(s,t,e,n){return Fh(s,t)+Oh(s,e)+Bh(s,n)}class zh extends Nh{constructor(t=new P,e=new P,n=new P){super(),this.isQuadraticBezierCurve3=!0,this.type="QuadraticBezierCurve3",this.v0=t,this.v1=e,this.v2=n}getPoint(t,e=new P){const n=e,i=this.v0,r=this.v1,a=this.v2;return n.set(_r(t,i.x,r.x,a.x),_r(t,i.y,r.y,a.y),_r(t,i.z,r.z,a.z)),n}copy(t){return super.copy(t),this.v0.copy(t.v0),this.v1.copy(t.v1),this.v2.copy(t.v2),this}toJSON(){const t=super.toJSON();return t.v0=this.v0.toArray(),t.v1=this.v1.toArray(),t.v2=this.v2.toArray(),t}fromJSON(t){return super.fromJSON(t),this.v0.fromArray(t.v0),this.v1.fromArray(t.v1),this.v2.fromArray(t.v2),this}}class Ba extends Oa{constructor(t=1,e=0){const n=(1+Math.sqrt(5))/2,i=[-1,n,0,1,n,0,-1,-n,0,1,-n,0,0,-1,n,0,1,n,0,-1,-n,0,1,-n,n,0,-1,n,0,1,-n,0,-1,-n,0,1],r=[0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1];super(i,r,t,e),this.type="IcosahedronGeometry",this.parameters={radius:t,detail:e}}static fromJSON(t){return new Ba(t.radius,t.detail)}}class Ji extends me{constructor(t=1,e=1,n=1,i=1){super(),this.type="PlaneGeometry",this.parameters={width:t,height:e,widthSegments:n,heightSegments:i};const r=t/2,a=e/2,o=Math.floor(n),l=Math.floor(i),c=o+1,h=l+1,d=t/o,u=e/l,p=[],g=[],v=[],m=[];for(let f=0;f<h;f++){const E=f*u-a;for(let b=0;b<c;b++){const S=b*d-r;g.push(S,-E,0),v.push(0,0,1),m.push(b/o),m.push(1-f/l)}}for(let f=0;f<l;f++)for(let E=0;E<o;E++){const b=E+c*f,S=E+c*(f+1),w=E+1+c*(f+1),A=E+1+c*f;p.push(b,S,A),p.push(S,w,A)}this.setIndex(p),this.setAttribute("position",new Xt(g,3)),this.setAttribute("normal",new Xt(v,3)),this.setAttribute("uv",new Xt(m,2))}copy(t){return super.copy(t),this.parameters=Object.assign({},t.parameters),this}static fromJSON(t){return new Ji(t.width,t.height,t.widthSegments,t.heightSegments)}}class za extends me{constructor(t=.5,e=1,n=32,i=1,r=0,a=Math.PI*2){super(),this.type="RingGeometry",this.parameters={innerRadius:t,outerRadius:e,thetaSegments:n,phiSegments:i,thetaStart:r,thetaLength:a},n=Math.max(3,n),i=Math.max(1,i);const o=[],l=[],c=[],h=[];let d=t;const u=(e-t)/i,p=new P,g=new dt;for(let v=0;v<=i;v++){for(let m=0;m<=n;m++){const f=r+m/n*a;p.x=d*Math.cos(f),p.y=d*Math.sin(f),l.push(p.x,p.y,p.z),c.push(0,0,1),g.x=(p.x/e+1)/2,g.y=(p.y/e+1)/2,h.push(g.x,g.y)}d+=u}for(let v=0;v<i;v++){const m=v*(n+1);for(let f=0;f<n;f++){const E=f+m,b=E,S=E+n+1,w=E+n+2,A=E+1;o.push(b,S,A),o.push(S,w,A)}}this.setIndex(o),this.setAttribute("position",new Xt(l,3)),this.setAttribute("normal",new Xt(c,3)),this.setAttribute("uv",new Xt(h,2))}copy(t){return super.copy(t),this.parameters=Object.assign({},t.parameters),this}static fromJSON(t){return new za(t.innerRadius,t.outerRadius,t.thetaSegments,t.phiSegments,t.thetaStart,t.thetaLength)}}class Vi extends me{constructor(t=1,e=32,n=16,i=0,r=Math.PI*2,a=0,o=Math.PI){super(),this.type="SphereGeometry",this.parameters={radius:t,widthSegments:e,heightSegments:n,phiStart:i,phiLength:r,thetaStart:a,thetaLength:o},e=Math.max(3,Math.floor(e)),n=Math.max(2,Math.floor(n));const l=Math.min(a+o,Math.PI);let c=0;const h=[],d=new P,u=new P,p=[],g=[],v=[],m=[];for(let f=0;f<=n;f++){const E=[],b=f/n;let S=0;f===0&&a===0?S=.5/e:f===n&&l===Math.PI&&(S=-.5/e);for(let w=0;w<=e;w++){const A=w/e;d.x=-t*Math.cos(i+A*r)*Math.sin(a+b*o),d.y=t*Math.cos(a+b*o),d.z=t*Math.sin(i+A*r)*Math.sin(a+b*o),g.push(d.x,d.y,d.z),u.copy(d).normalize(),v.push(u.x,u.y,u.z),m.push(A+S,1-b),E.push(c++)}h.push(E)}for(let f=0;f<n;f++)for(let E=0;E<e;E++){const b=h[f][E+1],S=h[f][E],w=h[f+1][E],A=h[f+1][E+1];(f!==0||a>0)&&p.push(b,S,A),(f!==n-1||l<Math.PI)&&p.push(S,w,A)}this.setIndex(p),this.setAttribute("position",new Xt(g,3)),this.setAttribute("normal",new Xt(v,3)),this.setAttribute("uv",new Xt(m,2))}copy(t){return super.copy(t),this.parameters=Object.assign({},t.parameters),this}static fromJSON(t){return new Vi(t.radius,t.widthSegments,t.heightSegments,t.phiStart,t.phiLength,t.thetaStart,t.thetaLength)}}class Gi extends me{constructor(t=1,e=.4,n=12,i=48,r=Math.PI*2){super(),this.type="TorusGeometry",this.parameters={radius:t,tube:e,radialSegments:n,tubularSegments:i,arc:r},n=Math.floor(n),i=Math.floor(i);const a=[],o=[],l=[],c=[],h=new P,d=new P,u=new P;for(let p=0;p<=n;p++)for(let g=0;g<=i;g++){const v=g/i*r,m=p/n*Math.PI*2;d.x=(t+e*Math.cos(m))*Math.cos(v),d.y=(t+e*Math.cos(m))*Math.sin(v),d.z=e*Math.sin(m),o.push(d.x,d.y,d.z),h.x=t*Math.cos(v),h.y=t*Math.sin(v),u.subVectors(d,h).normalize(),l.push(u.x,u.y,u.z),c.push(g/i),c.push(p/n)}for(let p=1;p<=n;p++)for(let g=1;g<=i;g++){const v=(i+1)*p+g-1,m=(i+1)*(p-1)+g-1,f=(i+1)*(p-1)+g,E=(i+1)*p+g;a.push(v,m,E),a.push(m,f,E)}this.setIndex(a),this.setAttribute("position",new Xt(o,3)),this.setAttribute("normal",new Xt(l,3)),this.setAttribute("uv",new Xt(c,2))}copy(t){return super.copy(t),this.parameters=Object.assign({},t.parameters),this}static fromJSON(t){return new Gi(t.radius,t.tube,t.radialSegments,t.tubularSegments,t.arc)}}class kh extends Ce{constructor(t){super(t),this.isRawShaderMaterial=!0,this.type="RawShaderMaterial"}}class Bi extends Qn{constructor(t){super(),this.isMeshStandardMaterial=!0,this.type="MeshStandardMaterial",this.defines={STANDARD:""},this.color=new Ft(16777215),this.roughness=1,this.metalness=0,this.map=null,this.lightMap=null,this.lightMapIntensity=1,this.aoMap=null,this.aoMapIntensity=1,this.emissive=new Ft(0),this.emissiveIntensity=1,this.emissiveMap=null,this.bumpMap=null,this.bumpScale=1,this.normalMap=null,this.normalMapType=Ll,this.normalScale=new dt(1,1),this.displacementMap=null,this.displacementScale=1,this.displacementBias=0,this.roughnessMap=null,this.metalnessMap=null,this.alphaMap=null,this.envMap=null,this.envMapRotation=new cn,this.envMapIntensity=1,this.wireframe=!1,this.wireframeLinewidth=1,this.wireframeLinecap="round",this.wireframeLinejoin="round",this.flatShading=!1,this.fog=!0,this.setValues(t)}copy(t){return super.copy(t),this.defines={STANDARD:""},this.color.copy(t.color),this.roughness=t.roughness,this.metalness=t.metalness,this.map=t.map,this.lightMap=t.lightMap,this.lightMapIntensity=t.lightMapIntensity,this.aoMap=t.aoMap,this.aoMapIntensity=t.aoMapIntensity,this.emissive.copy(t.emissive),this.emissiveMap=t.emissiveMap,this.emissiveIntensity=t.emissiveIntensity,this.bumpMap=t.bumpMap,this.bumpScale=t.bumpScale,this.normalMap=t.normalMap,this.normalMapType=t.normalMapType,this.normalScale.copy(t.normalScale),this.displacementMap=t.displacementMap,this.displacementScale=t.displacementScale,this.displacementBias=t.displacementBias,this.roughnessMap=t.roughnessMap,this.metalnessMap=t.metalnessMap,this.alphaMap=t.alphaMap,this.envMap=t.envMap,this.envMapRotation.copy(t.envMapRotation),this.envMapIntensity=t.envMapIntensity,this.wireframe=t.wireframe,this.wireframeLinewidth=t.wireframeLinewidth,this.wireframeLinecap=t.wireframeLinecap,this.wireframeLinejoin=t.wireframeLinejoin,this.flatShading=t.flatShading,this.fog=t.fog,this}}class Hh extends Qn{constructor(t){super(),this.isMeshDepthMaterial=!0,this.type="MeshDepthMaterial",this.depthPacking=Rc,this.map=null,this.alphaMap=null,this.displacementMap=null,this.displacementScale=1,this.displacementBias=0,this.wireframe=!1,this.wireframeLinewidth=1,this.setValues(t)}copy(t){return super.copy(t),this.depthPacking=t.depthPacking,this.map=t.map,this.alphaMap=t.alphaMap,this.displacementMap=t.displacementMap,this.displacementScale=t.displacementScale,this.displacementBias=t.displacementBias,this.wireframe=t.wireframe,this.wireframeLinewidth=t.wireframeLinewidth,this}}class Vh extends Qn{constructor(t){super(),this.isMeshDistanceMaterial=!0,this.type="MeshDistanceMaterial",this.map=null,this.alphaMap=null,this.displacementMap=null,this.displacementScale=1,this.displacementBias=0,this.setValues(t)}copy(t){return super.copy(t),this.map=t.map,this.alphaMap=t.alphaMap,this.displacementMap=t.displacementMap,this.displacementScale=t.displacementScale,this.displacementBias=t.displacementBias,this}}class Gh extends Hl{constructor(t){super(),this.isLineDashedMaterial=!0,this.type="LineDashedMaterial",this.scale=1,this.dashSize=3,this.gapSize=1,this.setValues(t)}copy(t){return super.copy(t),this.scale=t.scale,this.dashSize=t.dashSize,this.gapSize=t.gapSize,this}}class ka extends fe{constructor(t,e=1){super(),this.isLight=!0,this.type="Light",this.color=new Ft(t),this.intensity=e}dispose(){}copy(t,e){return super.copy(t,e),this.color.copy(t.color),this.intensity=t.intensity,this}toJSON(t){const e=super.toJSON(t);return e.object.color=this.color.getHex(),e.object.intensity=this.intensity,this.groundColor!==void 0&&(e.object.groundColor=this.groundColor.getHex()),this.distance!==void 0&&(e.object.distance=this.distance),this.angle!==void 0&&(e.object.angle=this.angle),this.decay!==void 0&&(e.object.decay=this.decay),this.penumbra!==void 0&&(e.object.penumbra=this.penumbra),this.shadow!==void 0&&(e.object.shadow=this.shadow.toJSON()),this.target!==void 0&&(e.object.target=this.target.uuid),e}}class Wh extends ka{constructor(t,e,n){super(t,n),this.isHemisphereLight=!0,this.type="HemisphereLight",this.position.copy(fe.DEFAULT_UP),this.updateMatrix(),this.groundColor=new Ft(e)}copy(t,e){return super.copy(t,e),this.groundColor.copy(t.groundColor),this}}const xr=new Kt,Co=new P,Ro=new P;class Xl{constructor(t){this.camera=t,this.intensity=1,this.bias=0,this.normalBias=0,this.radius=1,this.blurSamples=8,this.mapSize=new dt(512,512),this.mapType=ln,this.map=null,this.mapPass=null,this.matrix=new Kt,this.autoUpdate=!0,this.needsUpdate=!1,this._frustum=new Ua,this._frameExtents=new dt(1,1),this._viewportCount=1,this._viewports=[new Jt(0,0,1,1)]}getViewportCount(){return this._viewportCount}getFrustum(){return this._frustum}updateMatrices(t){const e=this.camera,n=this.matrix;Co.setFromMatrixPosition(t.matrixWorld),e.position.copy(Co),Ro.setFromMatrixPosition(t.target.matrixWorld),e.lookAt(Ro),e.updateMatrixWorld(),xr.multiplyMatrices(e.projectionMatrix,e.matrixWorldInverse),this._frustum.setFromProjectionMatrix(xr,e.coordinateSystem,e.reversedDepth),e.reversedDepth?n.set(.5,0,0,.5,0,.5,0,.5,0,0,1,0,0,0,0,1):n.set(.5,0,0,.5,0,.5,0,.5,0,0,.5,.5,0,0,0,1),n.multiply(xr)}getViewport(t){return this._viewports[t]}getFrameExtents(){return this._frameExtents}dispose(){this.map&&this.map.dispose(),this.mapPass&&this.mapPass.dispose()}copy(t){return this.camera=t.camera.clone(),this.intensity=t.intensity,this.bias=t.bias,this.radius=t.radius,this.autoUpdate=t.autoUpdate,this.needsUpdate=t.needsUpdate,this.normalBias=t.normalBias,this.blurSamples=t.blurSamples,this.mapSize.copy(t.mapSize),this}clone(){return new this.constructor().copy(this)}toJSON(){const t={};return this.intensity!==1&&(t.intensity=this.intensity),this.bias!==0&&(t.bias=this.bias),this.normalBias!==0&&(t.normalBias=this.normalBias),this.radius!==1&&(t.radius=this.radius),(this.mapSize.x!==512||this.mapSize.y!==512)&&(t.mapSize=this.mapSize.toArray()),t.camera=this.camera.toJSON(!1).object,delete t.camera.matrix,t}}const Po=new Kt,Oi=new P,vr=new P;class Xh extends Xl{constructor(){super(new We(90,1,.5,500)),this.isPointLightShadow=!0,this._frameExtents=new dt(4,2),this._viewportCount=6,this._viewports=[new Jt(2,1,1,1),new Jt(0,1,1,1),new Jt(3,1,1,1),new Jt(1,1,1,1),new Jt(3,0,1,1),new Jt(1,0,1,1)],this._cubeDirections=[new P(1,0,0),new P(-1,0,0),new P(0,0,1),new P(0,0,-1),new P(0,1,0),new P(0,-1,0)],this._cubeUps=[new P(0,1,0),new P(0,1,0),new P(0,1,0),new P(0,1,0),new P(0,0,1),new P(0,0,-1)]}updateMatrices(t,e=0){const n=this.camera,i=this.matrix,r=t.distance||n.far;r!==n.far&&(n.far=r,n.updateProjectionMatrix()),Oi.setFromMatrixPosition(t.matrixWorld),n.position.copy(Oi),vr.copy(n.position),vr.add(this._cubeDirections[e]),n.up.copy(this._cubeUps[e]),n.lookAt(vr),n.updateMatrixWorld(),i.makeTranslation(-Oi.x,-Oi.y,-Oi.z),Po.multiplyMatrices(n.projectionMatrix,n.matrixWorldInverse),this._frustum.setFromProjectionMatrix(Po,n.coordinateSystem,n.reversedDepth)}}class Lo extends ka{constructor(t,e,n=0,i=2){super(t,e),this.isPointLight=!0,this.type="PointLight",this.distance=n,this.decay=i,this.shadow=new Xh}get power(){return this.intensity*4*Math.PI}set power(t){this.intensity=t/(4*Math.PI)}dispose(){this.shadow.dispose()}copy(t,e){return super.copy(t,e),this.distance=t.distance,this.decay=t.decay,this.shadow=t.shadow.clone(),this}}class Gs extends zl{constructor(t=-1,e=1,n=1,i=-1,r=.1,a=2e3){super(),this.isOrthographicCamera=!0,this.type="OrthographicCamera",this.zoom=1,this.view=null,this.left=t,this.right=e,this.top=n,this.bottom=i,this.near=r,this.far=a,this.updateProjectionMatrix()}copy(t,e){return super.copy(t,e),this.left=t.left,this.right=t.right,this.top=t.top,this.bottom=t.bottom,this.near=t.near,this.far=t.far,this.zoom=t.zoom,this.view=t.view===null?null:Object.assign({},t.view),this}setViewOffset(t,e,n,i,r,a){this.view===null&&(this.view={enabled:!0,fullWidth:1,fullHeight:1,offsetX:0,offsetY:0,width:1,height:1}),this.view.enabled=!0,this.view.fullWidth=t,this.view.fullHeight=e,this.view.offsetX=n,this.view.offsetY=i,this.view.width=r,this.view.height=a,this.updateProjectionMatrix()}clearViewOffset(){this.view!==null&&(this.view.enabled=!1),this.updateProjectionMatrix()}updateProjectionMatrix(){const t=(this.right-this.left)/(2*this.zoom),e=(this.top-this.bottom)/(2*this.zoom),n=(this.right+this.left)/2,i=(this.top+this.bottom)/2;let r=n-t,a=n+t,o=i+e,l=i-e;if(this.view!==null&&this.view.enabled){const c=(this.right-this.left)/this.view.fullWidth/this.zoom,h=(this.top-this.bottom)/this.view.fullHeight/this.zoom;r+=c*this.view.offsetX,a=r+c*this.view.width,o-=h*this.view.offsetY,l=o-h*this.view.height}this.projectionMatrix.makeOrthographic(r,a,o,l,this.near,this.far,this.coordinateSystem,this.reversedDepth),this.projectionMatrixInverse.copy(this.projectionMatrix).invert()}toJSON(t){const e=super.toJSON(t);return e.object.zoom=this.zoom,e.object.left=this.left,e.object.right=this.right,e.object.top=this.top,e.object.bottom=this.bottom,e.object.near=this.near,e.object.far=this.far,this.view!==null&&(e.object.view=Object.assign({},this.view)),e}}class qh extends Xl{constructor(){super(new Gs(-5,5,5,-5,.5,500)),this.isDirectionalLightShadow=!0}}class Do extends ka{constructor(t,e){super(t,e),this.isDirectionalLight=!0,this.type="DirectionalLight",this.position.copy(fe.DEFAULT_UP),this.updateMatrix(),this.target=new fe,this.shadow=new qh}dispose(){this.shadow.dispose()}copy(t){return super.copy(t),this.target=t.target.clone(),this.shadow=t.shadow.clone(),this}}class Yh extends We{constructor(t=[]){super(),this.isArrayCamera=!0,this.isMultiViewCamera=!1,this.cameras=t}}class ql{constructor(t=!0){this.autoStart=t,this.startTime=0,this.oldTime=0,this.elapsedTime=0,this.running=!1}start(){this.startTime=performance.now(),this.oldTime=this.startTime,this.elapsedTime=0,this.running=!0}stop(){this.getElapsedTime(),this.running=!1,this.autoStart=!1}getElapsedTime(){return this.getDelta(),this.elapsedTime}getDelta(){let t=0;if(this.autoStart&&!this.running)return this.start(),0;if(this.running){const e=performance.now();t=(e-this.oldTime)/1e3,this.oldTime=e,this.elapsedTime+=t}return t}}const Io=new Kt;class jh{constructor(t,e,n=0,i=1/0){this.ray=new $i(t,e),this.near=n,this.far=i,this.camera=null,this.layers=new Da,this.params={Mesh:{},Line:{threshold:1},LOD:{},Points:{threshold:1},Sprite:{}}}set(t,e){this.ray.set(t,e)}setFromCamera(t,e){e.isPerspectiveCamera?(this.ray.origin.setFromMatrixPosition(e.matrixWorld),this.ray.direction.set(t.x,t.y,.5).unproject(e).sub(this.ray.origin).normalize(),this.camera=e):e.isOrthographicCamera?(this.ray.origin.set(t.x,t.y,(e.near+e.far)/(e.near-e.far)).unproject(e),this.ray.direction.set(0,0,-1).transformDirection(e.matrixWorld),this.camera=e):console.error("THREE.Raycaster: Unsupported camera type: "+e.type)}setFromXRController(t){return Io.identity().extractRotation(t.matrixWorld),this.ray.origin.setFromMatrixPosition(t.matrixWorld),this.ray.direction.set(0,0,-1).applyMatrix4(Io),this}intersectObject(t,e=!0,n=[]){return va(t,this,n,e),n.sort(Uo),n}intersectObjects(t,e=!0,n=[]){for(let i=0,r=t.length;i<r;i++)va(t[i],this,n,e);return n.sort(Uo),n}}function Uo(s,t){return s.distance-t.distance}function va(s,t,e,n){let i=!0;if(s.layers.test(t.layers)&&s.raycast(t,e)===!1&&(i=!1),i===!0&&n===!0){const r=s.children;for(let a=0,o=r.length;a<o;a++)va(r[a],t,e,!0)}}class No{constructor(t=1,e=0,n=0){this.radius=t,this.phi=e,this.theta=n}set(t,e,n){return this.radius=t,this.phi=e,this.theta=n,this}copy(t){return this.radius=t.radius,this.phi=t.phi,this.theta=t.theta,this}makeSafe(){return this.phi=zt(this.phi,1e-6,Math.PI-1e-6),this}setFromVector3(t){return this.setFromCartesianCoords(t.x,t.y,t.z)}setFromCartesianCoords(t,e,n){return this.radius=Math.sqrt(t*t+e*e+n*n),this.radius===0?(this.theta=0,this.phi=0):(this.theta=Math.atan2(t,n),this.phi=Math.acos(zt(e/this.radius,-1,1))),this}clone(){return new this.constructor().copy(this)}}class Kh extends Zn{constructor(t,e=null){super(),this.object=t,this.domElement=e,this.enabled=!0,this.state=-1,this.keys={},this.mouseButtons={LEFT:null,MIDDLE:null,RIGHT:null},this.touches={ONE:null,TWO:null}}connect(t){if(t===void 0){console.warn("THREE.Controls: connect() now requires an element.");return}this.domElement!==null&&this.disconnect(),this.domElement=t}disconnect(){}dispose(){}update(){}}function Fo(s,t,e,n){const i=Zh(n);switch(e){case Cl:return s*t;case wa:return s*t/i.components*i.byteLength;case Aa:return s*t/i.components*i.byteLength;case Pl:return s*t*2/i.components*i.byteLength;case Ca:return s*t*2/i.components*i.byteLength;case Rl:return s*t*3/i.components*i.byteLength;case Ze:return s*t*4/i.components*i.byteLength;case Ra:return s*t*4/i.components*i.byteLength;case Rs:case Ps:return Math.floor((s+3)/4)*Math.floor((t+3)/4)*8;case Ls:case Ds:return Math.floor((s+3)/4)*Math.floor((t+3)/4)*16;case Xr:case Yr:return Math.max(s,16)*Math.max(t,8)/4;case Wr:case qr:return Math.max(s,8)*Math.max(t,8)/2;case jr:case Kr:return Math.floor((s+3)/4)*Math.floor((t+3)/4)*8;case Zr:return Math.floor((s+3)/4)*Math.floor((t+3)/4)*16;case $r:return Math.floor((s+3)/4)*Math.floor((t+3)/4)*16;case Jr:return Math.floor((s+4)/5)*Math.floor((t+3)/4)*16;case Qr:return Math.floor((s+4)/5)*Math.floor((t+4)/5)*16;case ta:return Math.floor((s+5)/6)*Math.floor((t+4)/5)*16;case ea:return Math.floor((s+5)/6)*Math.floor((t+5)/6)*16;case na:return Math.floor((s+7)/8)*Math.floor((t+4)/5)*16;case ia:return Math.floor((s+7)/8)*Math.floor((t+5)/6)*16;case sa:return Math.floor((s+7)/8)*Math.floor((t+7)/8)*16;case ra:return Math.floor((s+9)/10)*Math.floor((t+4)/5)*16;case aa:return Math.floor((s+9)/10)*Math.floor((t+5)/6)*16;case oa:return Math.floor((s+9)/10)*Math.floor((t+7)/8)*16;case la:return Math.floor((s+9)/10)*Math.floor((t+9)/10)*16;case ca:return Math.floor((s+11)/12)*Math.floor((t+9)/10)*16;case ha:return Math.floor((s+11)/12)*Math.floor((t+11)/12)*16;case ua:case da:case fa:return Math.ceil(s/4)*Math.ceil(t/4)*16;case pa:case ma:return Math.ceil(s/4)*Math.ceil(t/4)*8;case ga:case _a:return Math.ceil(s/4)*Math.ceil(t/4)*16}throw new Error(`Unable to determine texture byte length for ${e} format.`)}function Zh(s){switch(s){case ln:case bl:return{byteLength:1,components:1};case Wi:case Tl:case yn:return{byteLength:2,components:1};case ba:case Ta:return{byteLength:2,components:4};case jn:case Ea:case an:return{byteLength:4,components:1};case wl:case Al:return{byteLength:4,components:3}}throw new Error(`Unknown texture type ${s}.`)}typeof __THREE_DEVTOOLS__<"u"&&__THREE_DEVTOOLS__.dispatchEvent(new CustomEvent("register",{detail:{revision:Ma}}));typeof window<"u"&&(window.__THREE__?console.warn("WARNING: Multiple instances of Three.js being imported."):window.__THREE__=Ma);function Yl(){let s=null,t=!1,e=null,n=null;function i(r,a){e(r,a),n=s.requestAnimationFrame(i)}return{start:function(){t!==!0&&e!==null&&(n=s.requestAnimationFrame(i),t=!0)},stop:function(){s.cancelAnimationFrame(n),t=!1},setAnimationLoop:function(r){e=r},setContext:function(r){s=r}}}function $h(s){const t=new WeakMap;function e(o,l){const c=o.array,h=o.usage,d=c.byteLength,u=s.createBuffer();s.bindBuffer(l,u),s.bufferData(l,c,h),o.onUploadCallback();let p;if(c instanceof Float32Array)p=s.FLOAT;else if(typeof Float16Array<"u"&&c instanceof Float16Array)p=s.HALF_FLOAT;else if(c instanceof Uint16Array)o.isFloat16BufferAttribute?p=s.HALF_FLOAT:p=s.UNSIGNED_SHORT;else if(c instanceof Int16Array)p=s.SHORT;else if(c instanceof Uint32Array)p=s.UNSIGNED_INT;else if(c instanceof Int32Array)p=s.INT;else if(c instanceof Int8Array)p=s.BYTE;else if(c instanceof Uint8Array)p=s.UNSIGNED_BYTE;else if(c instanceof Uint8ClampedArray)p=s.UNSIGNED_BYTE;else throw new Error("THREE.WebGLAttributes: Unsupported buffer data format: "+c);return{buffer:u,type:p,bytesPerElement:c.BYTES_PER_ELEMENT,version:o.version,size:d}}function n(o,l,c){const h=l.array,d=l.updateRanges;if(s.bindBuffer(c,o),d.length===0)s.bufferSubData(c,0,h);else{d.sort((p,g)=>p.start-g.start);let u=0;for(let p=1;p<d.length;p++){const g=d[u],v=d[p];v.start<=g.start+g.count+1?g.count=Math.max(g.count,v.start+v.count-g.start):(++u,d[u]=v)}d.length=u+1;for(let p=0,g=d.length;p<g;p++){const v=d[p];s.bufferSubData(c,v.start*h.BYTES_PER_ELEMENT,h,v.start,v.count)}l.clearUpdateRanges()}l.onUploadCallback()}function i(o){return o.isInterleavedBufferAttribute&&(o=o.data),t.get(o)}function r(o){o.isInterleavedBufferAttribute&&(o=o.data);const l=t.get(o);l&&(s.deleteBuffer(l.buffer),t.delete(o))}function a(o,l){if(o.isInterleavedBufferAttribute&&(o=o.data),o.isGLBufferAttribute){const h=t.get(o);(!h||h.version<o.version)&&t.set(o,{buffer:o.buffer,type:o.type,bytesPerElement:o.elementSize,version:o.version});return}const c=t.get(o);if(c===void 0)t.set(o,e(o,l));else if(c.version<o.version){if(c.size!==o.array.byteLength)throw new Error("THREE.WebGLAttributes: The size of the buffer attribute's array buffer does not match the original size. Resizing buffer attributes is not supported.");n(c.buffer,o,l),c.version=o.version}}return{get:i,remove:r,update:a}}var Jh=`#ifdef USE_ALPHAHASH
	if ( diffuseColor.a < getAlphaHashThreshold( vPosition ) ) discard;
#endif`,Qh=`#ifdef USE_ALPHAHASH
	const float ALPHA_HASH_SCALE = 0.05;
	float hash2D( vec2 value ) {
		return fract( 1.0e4 * sin( 17.0 * value.x + 0.1 * value.y ) * ( 0.1 + abs( sin( 13.0 * value.y + value.x ) ) ) );
	}
	float hash3D( vec3 value ) {
		return hash2D( vec2( hash2D( value.xy ), value.z ) );
	}
	float getAlphaHashThreshold( vec3 position ) {
		float maxDeriv = max(
			length( dFdx( position.xyz ) ),
			length( dFdy( position.xyz ) )
		);
		float pixScale = 1.0 / ( ALPHA_HASH_SCALE * maxDeriv );
		vec2 pixScales = vec2(
			exp2( floor( log2( pixScale ) ) ),
			exp2( ceil( log2( pixScale ) ) )
		);
		vec2 alpha = vec2(
			hash3D( floor( pixScales.x * position.xyz ) ),
			hash3D( floor( pixScales.y * position.xyz ) )
		);
		float lerpFactor = fract( log2( pixScale ) );
		float x = ( 1.0 - lerpFactor ) * alpha.x + lerpFactor * alpha.y;
		float a = min( lerpFactor, 1.0 - lerpFactor );
		vec3 cases = vec3(
			x * x / ( 2.0 * a * ( 1.0 - a ) ),
			( x - 0.5 * a ) / ( 1.0 - a ),
			1.0 - ( ( 1.0 - x ) * ( 1.0 - x ) / ( 2.0 * a * ( 1.0 - a ) ) )
		);
		float threshold = ( x < ( 1.0 - a ) )
			? ( ( x < a ) ? cases.x : cases.y )
			: cases.z;
		return clamp( threshold , 1.0e-6, 1.0 );
	}
#endif`,tu=`#ifdef USE_ALPHAMAP
	diffuseColor.a *= texture2D( alphaMap, vAlphaMapUv ).g;
#endif`,eu=`#ifdef USE_ALPHAMAP
	uniform sampler2D alphaMap;
#endif`,nu=`#ifdef USE_ALPHATEST
	#ifdef ALPHA_TO_COVERAGE
	diffuseColor.a = smoothstep( alphaTest, alphaTest + fwidth( diffuseColor.a ), diffuseColor.a );
	if ( diffuseColor.a == 0.0 ) discard;
	#else
	if ( diffuseColor.a < alphaTest ) discard;
	#endif
#endif`,iu=`#ifdef USE_ALPHATEST
	uniform float alphaTest;
#endif`,su=`#ifdef USE_AOMAP
	float ambientOcclusion = ( texture2D( aoMap, vAoMapUv ).r - 1.0 ) * aoMapIntensity + 1.0;
	reflectedLight.indirectDiffuse *= ambientOcclusion;
	#if defined( USE_CLEARCOAT ) 
		clearcoatSpecularIndirect *= ambientOcclusion;
	#endif
	#if defined( USE_SHEEN ) 
		sheenSpecularIndirect *= ambientOcclusion;
	#endif
	#if defined( USE_ENVMAP ) && defined( STANDARD )
		float dotNV = saturate( dot( geometryNormal, geometryViewDir ) );
		reflectedLight.indirectSpecular *= computeSpecularOcclusion( dotNV, ambientOcclusion, material.roughness );
	#endif
#endif`,ru=`#ifdef USE_AOMAP
	uniform sampler2D aoMap;
	uniform float aoMapIntensity;
#endif`,au=`#ifdef USE_BATCHING
	#if ! defined( GL_ANGLE_multi_draw )
	#define gl_DrawID _gl_DrawID
	uniform int _gl_DrawID;
	#endif
	uniform highp sampler2D batchingTexture;
	uniform highp usampler2D batchingIdTexture;
	mat4 getBatchingMatrix( const in float i ) {
		int size = textureSize( batchingTexture, 0 ).x;
		int j = int( i ) * 4;
		int x = j % size;
		int y = j / size;
		vec4 v1 = texelFetch( batchingTexture, ivec2( x, y ), 0 );
		vec4 v2 = texelFetch( batchingTexture, ivec2( x + 1, y ), 0 );
		vec4 v3 = texelFetch( batchingTexture, ivec2( x + 2, y ), 0 );
		vec4 v4 = texelFetch( batchingTexture, ivec2( x + 3, y ), 0 );
		return mat4( v1, v2, v3, v4 );
	}
	float getIndirectIndex( const in int i ) {
		int size = textureSize( batchingIdTexture, 0 ).x;
		int x = i % size;
		int y = i / size;
		return float( texelFetch( batchingIdTexture, ivec2( x, y ), 0 ).r );
	}
#endif
#ifdef USE_BATCHING_COLOR
	uniform sampler2D batchingColorTexture;
	vec3 getBatchingColor( const in float i ) {
		int size = textureSize( batchingColorTexture, 0 ).x;
		int j = int( i );
		int x = j % size;
		int y = j / size;
		return texelFetch( batchingColorTexture, ivec2( x, y ), 0 ).rgb;
	}
#endif`,ou=`#ifdef USE_BATCHING
	mat4 batchingMatrix = getBatchingMatrix( getIndirectIndex( gl_DrawID ) );
#endif`,lu=`vec3 transformed = vec3( position );
#ifdef USE_ALPHAHASH
	vPosition = vec3( position );
#endif`,cu=`vec3 objectNormal = vec3( normal );
#ifdef USE_TANGENT
	vec3 objectTangent = vec3( tangent.xyz );
#endif`,hu=`float G_BlinnPhong_Implicit( ) {
	return 0.25;
}
float D_BlinnPhong( const in float shininess, const in float dotNH ) {
	return RECIPROCAL_PI * ( shininess * 0.5 + 1.0 ) * pow( dotNH, shininess );
}
vec3 BRDF_BlinnPhong( const in vec3 lightDir, const in vec3 viewDir, const in vec3 normal, const in vec3 specularColor, const in float shininess ) {
	vec3 halfDir = normalize( lightDir + viewDir );
	float dotNH = saturate( dot( normal, halfDir ) );
	float dotVH = saturate( dot( viewDir, halfDir ) );
	vec3 F = F_Schlick( specularColor, 1.0, dotVH );
	float G = G_BlinnPhong_Implicit( );
	float D = D_BlinnPhong( shininess, dotNH );
	return F * ( G * D );
} // validated`,uu=`#ifdef USE_IRIDESCENCE
	const mat3 XYZ_TO_REC709 = mat3(
		 3.2404542, -0.9692660,  0.0556434,
		-1.5371385,  1.8760108, -0.2040259,
		-0.4985314,  0.0415560,  1.0572252
	);
	vec3 Fresnel0ToIor( vec3 fresnel0 ) {
		vec3 sqrtF0 = sqrt( fresnel0 );
		return ( vec3( 1.0 ) + sqrtF0 ) / ( vec3( 1.0 ) - sqrtF0 );
	}
	vec3 IorToFresnel0( vec3 transmittedIor, float incidentIor ) {
		return pow2( ( transmittedIor - vec3( incidentIor ) ) / ( transmittedIor + vec3( incidentIor ) ) );
	}
	float IorToFresnel0( float transmittedIor, float incidentIor ) {
		return pow2( ( transmittedIor - incidentIor ) / ( transmittedIor + incidentIor ));
	}
	vec3 evalSensitivity( float OPD, vec3 shift ) {
		float phase = 2.0 * PI * OPD * 1.0e-9;
		vec3 val = vec3( 5.4856e-13, 4.4201e-13, 5.2481e-13 );
		vec3 pos = vec3( 1.6810e+06, 1.7953e+06, 2.2084e+06 );
		vec3 var = vec3( 4.3278e+09, 9.3046e+09, 6.6121e+09 );
		vec3 xyz = val * sqrt( 2.0 * PI * var ) * cos( pos * phase + shift ) * exp( - pow2( phase ) * var );
		xyz.x += 9.7470e-14 * sqrt( 2.0 * PI * 4.5282e+09 ) * cos( 2.2399e+06 * phase + shift[ 0 ] ) * exp( - 4.5282e+09 * pow2( phase ) );
		xyz /= 1.0685e-7;
		vec3 rgb = XYZ_TO_REC709 * xyz;
		return rgb;
	}
	vec3 evalIridescence( float outsideIOR, float eta2, float cosTheta1, float thinFilmThickness, vec3 baseF0 ) {
		vec3 I;
		float iridescenceIOR = mix( outsideIOR, eta2, smoothstep( 0.0, 0.03, thinFilmThickness ) );
		float sinTheta2Sq = pow2( outsideIOR / iridescenceIOR ) * ( 1.0 - pow2( cosTheta1 ) );
		float cosTheta2Sq = 1.0 - sinTheta2Sq;
		if ( cosTheta2Sq < 0.0 ) {
			return vec3( 1.0 );
		}
		float cosTheta2 = sqrt( cosTheta2Sq );
		float R0 = IorToFresnel0( iridescenceIOR, outsideIOR );
		float R12 = F_Schlick( R0, 1.0, cosTheta1 );
		float T121 = 1.0 - R12;
		float phi12 = 0.0;
		if ( iridescenceIOR < outsideIOR ) phi12 = PI;
		float phi21 = PI - phi12;
		vec3 baseIOR = Fresnel0ToIor( clamp( baseF0, 0.0, 0.9999 ) );		vec3 R1 = IorToFresnel0( baseIOR, iridescenceIOR );
		vec3 R23 = F_Schlick( R1, 1.0, cosTheta2 );
		vec3 phi23 = vec3( 0.0 );
		if ( baseIOR[ 0 ] < iridescenceIOR ) phi23[ 0 ] = PI;
		if ( baseIOR[ 1 ] < iridescenceIOR ) phi23[ 1 ] = PI;
		if ( baseIOR[ 2 ] < iridescenceIOR ) phi23[ 2 ] = PI;
		float OPD = 2.0 * iridescenceIOR * thinFilmThickness * cosTheta2;
		vec3 phi = vec3( phi21 ) + phi23;
		vec3 R123 = clamp( R12 * R23, 1e-5, 0.9999 );
		vec3 r123 = sqrt( R123 );
		vec3 Rs = pow2( T121 ) * R23 / ( vec3( 1.0 ) - R123 );
		vec3 C0 = R12 + Rs;
		I = C0;
		vec3 Cm = Rs - T121;
		for ( int m = 1; m <= 2; ++ m ) {
			Cm *= r123;
			vec3 Sm = 2.0 * evalSensitivity( float( m ) * OPD, float( m ) * phi );
			I += Cm * Sm;
		}
		return max( I, vec3( 0.0 ) );
	}
#endif`,du=`#ifdef USE_BUMPMAP
	uniform sampler2D bumpMap;
	uniform float bumpScale;
	vec2 dHdxy_fwd() {
		vec2 dSTdx = dFdx( vBumpMapUv );
		vec2 dSTdy = dFdy( vBumpMapUv );
		float Hll = bumpScale * texture2D( bumpMap, vBumpMapUv ).x;
		float dBx = bumpScale * texture2D( bumpMap, vBumpMapUv + dSTdx ).x - Hll;
		float dBy = bumpScale * texture2D( bumpMap, vBumpMapUv + dSTdy ).x - Hll;
		return vec2( dBx, dBy );
	}
	vec3 perturbNormalArb( vec3 surf_pos, vec3 surf_norm, vec2 dHdxy, float faceDirection ) {
		vec3 vSigmaX = normalize( dFdx( surf_pos.xyz ) );
		vec3 vSigmaY = normalize( dFdy( surf_pos.xyz ) );
		vec3 vN = surf_norm;
		vec3 R1 = cross( vSigmaY, vN );
		vec3 R2 = cross( vN, vSigmaX );
		float fDet = dot( vSigmaX, R1 ) * faceDirection;
		vec3 vGrad = sign( fDet ) * ( dHdxy.x * R1 + dHdxy.y * R2 );
		return normalize( abs( fDet ) * surf_norm - vGrad );
	}
#endif`,fu=`#if NUM_CLIPPING_PLANES > 0
	vec4 plane;
	#ifdef ALPHA_TO_COVERAGE
		float distanceToPlane, distanceGradient;
		float clipOpacity = 1.0;
		#pragma unroll_loop_start
		for ( int i = 0; i < UNION_CLIPPING_PLANES; i ++ ) {
			plane = clippingPlanes[ i ];
			distanceToPlane = - dot( vClipPosition, plane.xyz ) + plane.w;
			distanceGradient = fwidth( distanceToPlane ) / 2.0;
			clipOpacity *= smoothstep( - distanceGradient, distanceGradient, distanceToPlane );
			if ( clipOpacity == 0.0 ) discard;
		}
		#pragma unroll_loop_end
		#if UNION_CLIPPING_PLANES < NUM_CLIPPING_PLANES
			float unionClipOpacity = 1.0;
			#pragma unroll_loop_start
			for ( int i = UNION_CLIPPING_PLANES; i < NUM_CLIPPING_PLANES; i ++ ) {
				plane = clippingPlanes[ i ];
				distanceToPlane = - dot( vClipPosition, plane.xyz ) + plane.w;
				distanceGradient = fwidth( distanceToPlane ) / 2.0;
				unionClipOpacity *= 1.0 - smoothstep( - distanceGradient, distanceGradient, distanceToPlane );
			}
			#pragma unroll_loop_end
			clipOpacity *= 1.0 - unionClipOpacity;
		#endif
		diffuseColor.a *= clipOpacity;
		if ( diffuseColor.a == 0.0 ) discard;
	#else
		#pragma unroll_loop_start
		for ( int i = 0; i < UNION_CLIPPING_PLANES; i ++ ) {
			plane = clippingPlanes[ i ];
			if ( dot( vClipPosition, plane.xyz ) > plane.w ) discard;
		}
		#pragma unroll_loop_end
		#if UNION_CLIPPING_PLANES < NUM_CLIPPING_PLANES
			bool clipped = true;
			#pragma unroll_loop_start
			for ( int i = UNION_CLIPPING_PLANES; i < NUM_CLIPPING_PLANES; i ++ ) {
				plane = clippingPlanes[ i ];
				clipped = ( dot( vClipPosition, plane.xyz ) > plane.w ) && clipped;
			}
			#pragma unroll_loop_end
			if ( clipped ) discard;
		#endif
	#endif
#endif`,pu=`#if NUM_CLIPPING_PLANES > 0
	varying vec3 vClipPosition;
	uniform vec4 clippingPlanes[ NUM_CLIPPING_PLANES ];
#endif`,mu=`#if NUM_CLIPPING_PLANES > 0
	varying vec3 vClipPosition;
#endif`,gu=`#if NUM_CLIPPING_PLANES > 0
	vClipPosition = - mvPosition.xyz;
#endif`,_u=`#if defined( USE_COLOR_ALPHA )
	diffuseColor *= vColor;
#elif defined( USE_COLOR )
	diffuseColor.rgb *= vColor;
#endif`,xu=`#if defined( USE_COLOR_ALPHA )
	varying vec4 vColor;
#elif defined( USE_COLOR )
	varying vec3 vColor;
#endif`,vu=`#if defined( USE_COLOR_ALPHA )
	varying vec4 vColor;
#elif defined( USE_COLOR ) || defined( USE_INSTANCING_COLOR ) || defined( USE_BATCHING_COLOR )
	varying vec3 vColor;
#endif`,yu=`#if defined( USE_COLOR_ALPHA )
	vColor = vec4( 1.0 );
#elif defined( USE_COLOR ) || defined( USE_INSTANCING_COLOR ) || defined( USE_BATCHING_COLOR )
	vColor = vec3( 1.0 );
#endif
#ifdef USE_COLOR
	vColor *= color;
#endif
#ifdef USE_INSTANCING_COLOR
	vColor.xyz *= instanceColor.xyz;
#endif
#ifdef USE_BATCHING_COLOR
	vec3 batchingColor = getBatchingColor( getIndirectIndex( gl_DrawID ) );
	vColor.xyz *= batchingColor.xyz;
#endif`,Mu=`#define PI 3.141592653589793
#define PI2 6.283185307179586
#define PI_HALF 1.5707963267948966
#define RECIPROCAL_PI 0.3183098861837907
#define RECIPROCAL_PI2 0.15915494309189535
#define EPSILON 1e-6
#ifndef saturate
#define saturate( a ) clamp( a, 0.0, 1.0 )
#endif
#define whiteComplement( a ) ( 1.0 - saturate( a ) )
float pow2( const in float x ) { return x*x; }
vec3 pow2( const in vec3 x ) { return x*x; }
float pow3( const in float x ) { return x*x*x; }
float pow4( const in float x ) { float x2 = x*x; return x2*x2; }
float max3( const in vec3 v ) { return max( max( v.x, v.y ), v.z ); }
float average( const in vec3 v ) { return dot( v, vec3( 0.3333333 ) ); }
highp float rand( const in vec2 uv ) {
	const highp float a = 12.9898, b = 78.233, c = 43758.5453;
	highp float dt = dot( uv.xy, vec2( a,b ) ), sn = mod( dt, PI );
	return fract( sin( sn ) * c );
}
#ifdef HIGH_PRECISION
	float precisionSafeLength( vec3 v ) { return length( v ); }
#else
	float precisionSafeLength( vec3 v ) {
		float maxComponent = max3( abs( v ) );
		return length( v / maxComponent ) * maxComponent;
	}
#endif
struct IncidentLight {
	vec3 color;
	vec3 direction;
	bool visible;
};
struct ReflectedLight {
	vec3 directDiffuse;
	vec3 directSpecular;
	vec3 indirectDiffuse;
	vec3 indirectSpecular;
};
#ifdef USE_ALPHAHASH
	varying vec3 vPosition;
#endif
vec3 transformDirection( in vec3 dir, in mat4 matrix ) {
	return normalize( ( matrix * vec4( dir, 0.0 ) ).xyz );
}
vec3 inverseTransformDirection( in vec3 dir, in mat4 matrix ) {
	return normalize( ( vec4( dir, 0.0 ) * matrix ).xyz );
}
mat3 transposeMat3( const in mat3 m ) {
	mat3 tmp;
	tmp[ 0 ] = vec3( m[ 0 ].x, m[ 1 ].x, m[ 2 ].x );
	tmp[ 1 ] = vec3( m[ 0 ].y, m[ 1 ].y, m[ 2 ].y );
	tmp[ 2 ] = vec3( m[ 0 ].z, m[ 1 ].z, m[ 2 ].z );
	return tmp;
}
bool isPerspectiveMatrix( mat4 m ) {
	return m[ 2 ][ 3 ] == - 1.0;
}
vec2 equirectUv( in vec3 dir ) {
	float u = atan( dir.z, dir.x ) * RECIPROCAL_PI2 + 0.5;
	float v = asin( clamp( dir.y, - 1.0, 1.0 ) ) * RECIPROCAL_PI + 0.5;
	return vec2( u, v );
}
vec3 BRDF_Lambert( const in vec3 diffuseColor ) {
	return RECIPROCAL_PI * diffuseColor;
}
vec3 F_Schlick( const in vec3 f0, const in float f90, const in float dotVH ) {
	float fresnel = exp2( ( - 5.55473 * dotVH - 6.98316 ) * dotVH );
	return f0 * ( 1.0 - fresnel ) + ( f90 * fresnel );
}
float F_Schlick( const in float f0, const in float f90, const in float dotVH ) {
	float fresnel = exp2( ( - 5.55473 * dotVH - 6.98316 ) * dotVH );
	return f0 * ( 1.0 - fresnel ) + ( f90 * fresnel );
} // validated`,Su=`#ifdef ENVMAP_TYPE_CUBE_UV
	#define cubeUV_minMipLevel 4.0
	#define cubeUV_minTileSize 16.0
	float getFace( vec3 direction ) {
		vec3 absDirection = abs( direction );
		float face = - 1.0;
		if ( absDirection.x > absDirection.z ) {
			if ( absDirection.x > absDirection.y )
				face = direction.x > 0.0 ? 0.0 : 3.0;
			else
				face = direction.y > 0.0 ? 1.0 : 4.0;
		} else {
			if ( absDirection.z > absDirection.y )
				face = direction.z > 0.0 ? 2.0 : 5.0;
			else
				face = direction.y > 0.0 ? 1.0 : 4.0;
		}
		return face;
	}
	vec2 getUV( vec3 direction, float face ) {
		vec2 uv;
		if ( face == 0.0 ) {
			uv = vec2( direction.z, direction.y ) / abs( direction.x );
		} else if ( face == 1.0 ) {
			uv = vec2( - direction.x, - direction.z ) / abs( direction.y );
		} else if ( face == 2.0 ) {
			uv = vec2( - direction.x, direction.y ) / abs( direction.z );
		} else if ( face == 3.0 ) {
			uv = vec2( - direction.z, direction.y ) / abs( direction.x );
		} else if ( face == 4.0 ) {
			uv = vec2( - direction.x, direction.z ) / abs( direction.y );
		} else {
			uv = vec2( direction.x, direction.y ) / abs( direction.z );
		}
		return 0.5 * ( uv + 1.0 );
	}
	vec3 bilinearCubeUV( sampler2D envMap, vec3 direction, float mipInt ) {
		float face = getFace( direction );
		float filterInt = max( cubeUV_minMipLevel - mipInt, 0.0 );
		mipInt = max( mipInt, cubeUV_minMipLevel );
		float faceSize = exp2( mipInt );
		highp vec2 uv = getUV( direction, face ) * ( faceSize - 2.0 ) + 1.0;
		if ( face > 2.0 ) {
			uv.y += faceSize;
			face -= 3.0;
		}
		uv.x += face * faceSize;
		uv.x += filterInt * 3.0 * cubeUV_minTileSize;
		uv.y += 4.0 * ( exp2( CUBEUV_MAX_MIP ) - faceSize );
		uv.x *= CUBEUV_TEXEL_WIDTH;
		uv.y *= CUBEUV_TEXEL_HEIGHT;
		#ifdef texture2DGradEXT
			return texture2DGradEXT( envMap, uv, vec2( 0.0 ), vec2( 0.0 ) ).rgb;
		#else
			return texture2D( envMap, uv ).rgb;
		#endif
	}
	#define cubeUV_r0 1.0
	#define cubeUV_m0 - 2.0
	#define cubeUV_r1 0.8
	#define cubeUV_m1 - 1.0
	#define cubeUV_r4 0.4
	#define cubeUV_m4 2.0
	#define cubeUV_r5 0.305
	#define cubeUV_m5 3.0
	#define cubeUV_r6 0.21
	#define cubeUV_m6 4.0
	float roughnessToMip( float roughness ) {
		float mip = 0.0;
		if ( roughness >= cubeUV_r1 ) {
			mip = ( cubeUV_r0 - roughness ) * ( cubeUV_m1 - cubeUV_m0 ) / ( cubeUV_r0 - cubeUV_r1 ) + cubeUV_m0;
		} else if ( roughness >= cubeUV_r4 ) {
			mip = ( cubeUV_r1 - roughness ) * ( cubeUV_m4 - cubeUV_m1 ) / ( cubeUV_r1 - cubeUV_r4 ) + cubeUV_m1;
		} else if ( roughness >= cubeUV_r5 ) {
			mip = ( cubeUV_r4 - roughness ) * ( cubeUV_m5 - cubeUV_m4 ) / ( cubeUV_r4 - cubeUV_r5 ) + cubeUV_m4;
		} else if ( roughness >= cubeUV_r6 ) {
			mip = ( cubeUV_r5 - roughness ) * ( cubeUV_m6 - cubeUV_m5 ) / ( cubeUV_r5 - cubeUV_r6 ) + cubeUV_m5;
		} else {
			mip = - 2.0 * log2( 1.16 * roughness );		}
		return mip;
	}
	vec4 textureCubeUV( sampler2D envMap, vec3 sampleDir, float roughness ) {
		float mip = clamp( roughnessToMip( roughness ), cubeUV_m0, CUBEUV_MAX_MIP );
		float mipF = fract( mip );
		float mipInt = floor( mip );
		vec3 color0 = bilinearCubeUV( envMap, sampleDir, mipInt );
		if ( mipF == 0.0 ) {
			return vec4( color0, 1.0 );
		} else {
			vec3 color1 = bilinearCubeUV( envMap, sampleDir, mipInt + 1.0 );
			return vec4( mix( color0, color1, mipF ), 1.0 );
		}
	}
#endif`,Eu=`vec3 transformedNormal = objectNormal;
#ifdef USE_TANGENT
	vec3 transformedTangent = objectTangent;
#endif
#ifdef USE_BATCHING
	mat3 bm = mat3( batchingMatrix );
	transformedNormal /= vec3( dot( bm[ 0 ], bm[ 0 ] ), dot( bm[ 1 ], bm[ 1 ] ), dot( bm[ 2 ], bm[ 2 ] ) );
	transformedNormal = bm * transformedNormal;
	#ifdef USE_TANGENT
		transformedTangent = bm * transformedTangent;
	#endif
#endif
#ifdef USE_INSTANCING
	mat3 im = mat3( instanceMatrix );
	transformedNormal /= vec3( dot( im[ 0 ], im[ 0 ] ), dot( im[ 1 ], im[ 1 ] ), dot( im[ 2 ], im[ 2 ] ) );
	transformedNormal = im * transformedNormal;
	#ifdef USE_TANGENT
		transformedTangent = im * transformedTangent;
	#endif
#endif
transformedNormal = normalMatrix * transformedNormal;
#ifdef FLIP_SIDED
	transformedNormal = - transformedNormal;
#endif
#ifdef USE_TANGENT
	transformedTangent = ( modelViewMatrix * vec4( transformedTangent, 0.0 ) ).xyz;
	#ifdef FLIP_SIDED
		transformedTangent = - transformedTangent;
	#endif
#endif`,bu=`#ifdef USE_DISPLACEMENTMAP
	uniform sampler2D displacementMap;
	uniform float displacementScale;
	uniform float displacementBias;
#endif`,Tu=`#ifdef USE_DISPLACEMENTMAP
	transformed += normalize( objectNormal ) * ( texture2D( displacementMap, vDisplacementMapUv ).x * displacementScale + displacementBias );
#endif`,wu=`#ifdef USE_EMISSIVEMAP
	vec4 emissiveColor = texture2D( emissiveMap, vEmissiveMapUv );
	#ifdef DECODE_VIDEO_TEXTURE_EMISSIVE
		emissiveColor = sRGBTransferEOTF( emissiveColor );
	#endif
	totalEmissiveRadiance *= emissiveColor.rgb;
#endif`,Au=`#ifdef USE_EMISSIVEMAP
	uniform sampler2D emissiveMap;
#endif`,Cu="gl_FragColor = linearToOutputTexel( gl_FragColor );",Ru=`vec4 LinearTransferOETF( in vec4 value ) {
	return value;
}
vec4 sRGBTransferEOTF( in vec4 value ) {
	return vec4( mix( pow( value.rgb * 0.9478672986 + vec3( 0.0521327014 ), vec3( 2.4 ) ), value.rgb * 0.0773993808, vec3( lessThanEqual( value.rgb, vec3( 0.04045 ) ) ) ), value.a );
}
vec4 sRGBTransferOETF( in vec4 value ) {
	return vec4( mix( pow( value.rgb, vec3( 0.41666 ) ) * 1.055 - vec3( 0.055 ), value.rgb * 12.92, vec3( lessThanEqual( value.rgb, vec3( 0.0031308 ) ) ) ), value.a );
}`,Pu=`#ifdef USE_ENVMAP
	#ifdef ENV_WORLDPOS
		vec3 cameraToFrag;
		if ( isOrthographic ) {
			cameraToFrag = normalize( vec3( - viewMatrix[ 0 ][ 2 ], - viewMatrix[ 1 ][ 2 ], - viewMatrix[ 2 ][ 2 ] ) );
		} else {
			cameraToFrag = normalize( vWorldPosition - cameraPosition );
		}
		vec3 worldNormal = inverseTransformDirection( normal, viewMatrix );
		#ifdef ENVMAP_MODE_REFLECTION
			vec3 reflectVec = reflect( cameraToFrag, worldNormal );
		#else
			vec3 reflectVec = refract( cameraToFrag, worldNormal, refractionRatio );
		#endif
	#else
		vec3 reflectVec = vReflect;
	#endif
	#ifdef ENVMAP_TYPE_CUBE
		vec4 envColor = textureCube( envMap, envMapRotation * vec3( flipEnvMap * reflectVec.x, reflectVec.yz ) );
	#else
		vec4 envColor = vec4( 0.0 );
	#endif
	#ifdef ENVMAP_BLENDING_MULTIPLY
		outgoingLight = mix( outgoingLight, outgoingLight * envColor.xyz, specularStrength * reflectivity );
	#elif defined( ENVMAP_BLENDING_MIX )
		outgoingLight = mix( outgoingLight, envColor.xyz, specularStrength * reflectivity );
	#elif defined( ENVMAP_BLENDING_ADD )
		outgoingLight += envColor.xyz * specularStrength * reflectivity;
	#endif
#endif`,Lu=`#ifdef USE_ENVMAP
	uniform float envMapIntensity;
	uniform float flipEnvMap;
	uniform mat3 envMapRotation;
	#ifdef ENVMAP_TYPE_CUBE
		uniform samplerCube envMap;
	#else
		uniform sampler2D envMap;
	#endif
	
#endif`,Du=`#ifdef USE_ENVMAP
	uniform float reflectivity;
	#if defined( USE_BUMPMAP ) || defined( USE_NORMALMAP ) || defined( PHONG ) || defined( LAMBERT )
		#define ENV_WORLDPOS
	#endif
	#ifdef ENV_WORLDPOS
		varying vec3 vWorldPosition;
		uniform float refractionRatio;
	#else
		varying vec3 vReflect;
	#endif
#endif`,Iu=`#ifdef USE_ENVMAP
	#if defined( USE_BUMPMAP ) || defined( USE_NORMALMAP ) || defined( PHONG ) || defined( LAMBERT )
		#define ENV_WORLDPOS
	#endif
	#ifdef ENV_WORLDPOS
		
		varying vec3 vWorldPosition;
	#else
		varying vec3 vReflect;
		uniform float refractionRatio;
	#endif
#endif`,Uu=`#ifdef USE_ENVMAP
	#ifdef ENV_WORLDPOS
		vWorldPosition = worldPosition.xyz;
	#else
		vec3 cameraToVertex;
		if ( isOrthographic ) {
			cameraToVertex = normalize( vec3( - viewMatrix[ 0 ][ 2 ], - viewMatrix[ 1 ][ 2 ], - viewMatrix[ 2 ][ 2 ] ) );
		} else {
			cameraToVertex = normalize( worldPosition.xyz - cameraPosition );
		}
		vec3 worldNormal = inverseTransformDirection( transformedNormal, viewMatrix );
		#ifdef ENVMAP_MODE_REFLECTION
			vReflect = reflect( cameraToVertex, worldNormal );
		#else
			vReflect = refract( cameraToVertex, worldNormal, refractionRatio );
		#endif
	#endif
#endif`,Nu=`#ifdef USE_FOG
	vFogDepth = - mvPosition.z;
#endif`,Fu=`#ifdef USE_FOG
	varying float vFogDepth;
#endif`,Ou=`#ifdef USE_FOG
	#ifdef FOG_EXP2
		float fogFactor = 1.0 - exp( - fogDensity * fogDensity * vFogDepth * vFogDepth );
	#else
		float fogFactor = smoothstep( fogNear, fogFar, vFogDepth );
	#endif
	gl_FragColor.rgb = mix( gl_FragColor.rgb, fogColor, fogFactor );
#endif`,Bu=`#ifdef USE_FOG
	uniform vec3 fogColor;
	varying float vFogDepth;
	#ifdef FOG_EXP2
		uniform float fogDensity;
	#else
		uniform float fogNear;
		uniform float fogFar;
	#endif
#endif`,zu=`#ifdef USE_GRADIENTMAP
	uniform sampler2D gradientMap;
#endif
vec3 getGradientIrradiance( vec3 normal, vec3 lightDirection ) {
	float dotNL = dot( normal, lightDirection );
	vec2 coord = vec2( dotNL * 0.5 + 0.5, 0.0 );
	#ifdef USE_GRADIENTMAP
		return vec3( texture2D( gradientMap, coord ).r );
	#else
		vec2 fw = fwidth( coord ) * 0.5;
		return mix( vec3( 0.7 ), vec3( 1.0 ), smoothstep( 0.7 - fw.x, 0.7 + fw.x, coord.x ) );
	#endif
}`,ku=`#ifdef USE_LIGHTMAP
	uniform sampler2D lightMap;
	uniform float lightMapIntensity;
#endif`,Hu=`LambertMaterial material;
material.diffuseColor = diffuseColor.rgb;
material.specularStrength = specularStrength;`,Vu=`varying vec3 vViewPosition;
struct LambertMaterial {
	vec3 diffuseColor;
	float specularStrength;
};
void RE_Direct_Lambert( const in IncidentLight directLight, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in LambertMaterial material, inout ReflectedLight reflectedLight ) {
	float dotNL = saturate( dot( geometryNormal, directLight.direction ) );
	vec3 irradiance = dotNL * directLight.color;
	reflectedLight.directDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
void RE_IndirectDiffuse_Lambert( const in vec3 irradiance, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in LambertMaterial material, inout ReflectedLight reflectedLight ) {
	reflectedLight.indirectDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
#define RE_Direct				RE_Direct_Lambert
#define RE_IndirectDiffuse		RE_IndirectDiffuse_Lambert`,Gu=`uniform bool receiveShadow;
uniform vec3 ambientLightColor;
#if defined( USE_LIGHT_PROBES )
	uniform vec3 lightProbe[ 9 ];
#endif
vec3 shGetIrradianceAt( in vec3 normal, in vec3 shCoefficients[ 9 ] ) {
	float x = normal.x, y = normal.y, z = normal.z;
	vec3 result = shCoefficients[ 0 ] * 0.886227;
	result += shCoefficients[ 1 ] * 2.0 * 0.511664 * y;
	result += shCoefficients[ 2 ] * 2.0 * 0.511664 * z;
	result += shCoefficients[ 3 ] * 2.0 * 0.511664 * x;
	result += shCoefficients[ 4 ] * 2.0 * 0.429043 * x * y;
	result += shCoefficients[ 5 ] * 2.0 * 0.429043 * y * z;
	result += shCoefficients[ 6 ] * ( 0.743125 * z * z - 0.247708 );
	result += shCoefficients[ 7 ] * 2.0 * 0.429043 * x * z;
	result += shCoefficients[ 8 ] * 0.429043 * ( x * x - y * y );
	return result;
}
vec3 getLightProbeIrradiance( const in vec3 lightProbe[ 9 ], const in vec3 normal ) {
	vec3 worldNormal = inverseTransformDirection( normal, viewMatrix );
	vec3 irradiance = shGetIrradianceAt( worldNormal, lightProbe );
	return irradiance;
}
vec3 getAmbientLightIrradiance( const in vec3 ambientLightColor ) {
	vec3 irradiance = ambientLightColor;
	return irradiance;
}
float getDistanceAttenuation( const in float lightDistance, const in float cutoffDistance, const in float decayExponent ) {
	float distanceFalloff = 1.0 / max( pow( lightDistance, decayExponent ), 0.01 );
	if ( cutoffDistance > 0.0 ) {
		distanceFalloff *= pow2( saturate( 1.0 - pow4( lightDistance / cutoffDistance ) ) );
	}
	return distanceFalloff;
}
float getSpotAttenuation( const in float coneCosine, const in float penumbraCosine, const in float angleCosine ) {
	return smoothstep( coneCosine, penumbraCosine, angleCosine );
}
#if NUM_DIR_LIGHTS > 0
	struct DirectionalLight {
		vec3 direction;
		vec3 color;
	};
	uniform DirectionalLight directionalLights[ NUM_DIR_LIGHTS ];
	void getDirectionalLightInfo( const in DirectionalLight directionalLight, out IncidentLight light ) {
		light.color = directionalLight.color;
		light.direction = directionalLight.direction;
		light.visible = true;
	}
#endif
#if NUM_POINT_LIGHTS > 0
	struct PointLight {
		vec3 position;
		vec3 color;
		float distance;
		float decay;
	};
	uniform PointLight pointLights[ NUM_POINT_LIGHTS ];
	void getPointLightInfo( const in PointLight pointLight, const in vec3 geometryPosition, out IncidentLight light ) {
		vec3 lVector = pointLight.position - geometryPosition;
		light.direction = normalize( lVector );
		float lightDistance = length( lVector );
		light.color = pointLight.color;
		light.color *= getDistanceAttenuation( lightDistance, pointLight.distance, pointLight.decay );
		light.visible = ( light.color != vec3( 0.0 ) );
	}
#endif
#if NUM_SPOT_LIGHTS > 0
	struct SpotLight {
		vec3 position;
		vec3 direction;
		vec3 color;
		float distance;
		float decay;
		float coneCos;
		float penumbraCos;
	};
	uniform SpotLight spotLights[ NUM_SPOT_LIGHTS ];
	void getSpotLightInfo( const in SpotLight spotLight, const in vec3 geometryPosition, out IncidentLight light ) {
		vec3 lVector = spotLight.position - geometryPosition;
		light.direction = normalize( lVector );
		float angleCos = dot( light.direction, spotLight.direction );
		float spotAttenuation = getSpotAttenuation( spotLight.coneCos, spotLight.penumbraCos, angleCos );
		if ( spotAttenuation > 0.0 ) {
			float lightDistance = length( lVector );
			light.color = spotLight.color * spotAttenuation;
			light.color *= getDistanceAttenuation( lightDistance, spotLight.distance, spotLight.decay );
			light.visible = ( light.color != vec3( 0.0 ) );
		} else {
			light.color = vec3( 0.0 );
			light.visible = false;
		}
	}
#endif
#if NUM_RECT_AREA_LIGHTS > 0
	struct RectAreaLight {
		vec3 color;
		vec3 position;
		vec3 halfWidth;
		vec3 halfHeight;
	};
	uniform sampler2D ltc_1;	uniform sampler2D ltc_2;
	uniform RectAreaLight rectAreaLights[ NUM_RECT_AREA_LIGHTS ];
#endif
#if NUM_HEMI_LIGHTS > 0
	struct HemisphereLight {
		vec3 direction;
		vec3 skyColor;
		vec3 groundColor;
	};
	uniform HemisphereLight hemisphereLights[ NUM_HEMI_LIGHTS ];
	vec3 getHemisphereLightIrradiance( const in HemisphereLight hemiLight, const in vec3 normal ) {
		float dotNL = dot( normal, hemiLight.direction );
		float hemiDiffuseWeight = 0.5 * dotNL + 0.5;
		vec3 irradiance = mix( hemiLight.groundColor, hemiLight.skyColor, hemiDiffuseWeight );
		return irradiance;
	}
#endif`,Wu=`#ifdef USE_ENVMAP
	vec3 getIBLIrradiance( const in vec3 normal ) {
		#ifdef ENVMAP_TYPE_CUBE_UV
			vec3 worldNormal = inverseTransformDirection( normal, viewMatrix );
			vec4 envMapColor = textureCubeUV( envMap, envMapRotation * worldNormal, 1.0 );
			return PI * envMapColor.rgb * envMapIntensity;
		#else
			return vec3( 0.0 );
		#endif
	}
	vec3 getIBLRadiance( const in vec3 viewDir, const in vec3 normal, const in float roughness ) {
		#ifdef ENVMAP_TYPE_CUBE_UV
			vec3 reflectVec = reflect( - viewDir, normal );
			reflectVec = normalize( mix( reflectVec, normal, roughness * roughness) );
			reflectVec = inverseTransformDirection( reflectVec, viewMatrix );
			vec4 envMapColor = textureCubeUV( envMap, envMapRotation * reflectVec, roughness );
			return envMapColor.rgb * envMapIntensity;
		#else
			return vec3( 0.0 );
		#endif
	}
	#ifdef USE_ANISOTROPY
		vec3 getIBLAnisotropyRadiance( const in vec3 viewDir, const in vec3 normal, const in float roughness, const in vec3 bitangent, const in float anisotropy ) {
			#ifdef ENVMAP_TYPE_CUBE_UV
				vec3 bentNormal = cross( bitangent, viewDir );
				bentNormal = normalize( cross( bentNormal, bitangent ) );
				bentNormal = normalize( mix( bentNormal, normal, pow2( pow2( 1.0 - anisotropy * ( 1.0 - roughness ) ) ) ) );
				return getIBLRadiance( viewDir, bentNormal, roughness );
			#else
				return vec3( 0.0 );
			#endif
		}
	#endif
#endif`,Xu=`ToonMaterial material;
material.diffuseColor = diffuseColor.rgb;`,qu=`varying vec3 vViewPosition;
struct ToonMaterial {
	vec3 diffuseColor;
};
void RE_Direct_Toon( const in IncidentLight directLight, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in ToonMaterial material, inout ReflectedLight reflectedLight ) {
	vec3 irradiance = getGradientIrradiance( geometryNormal, directLight.direction ) * directLight.color;
	reflectedLight.directDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
void RE_IndirectDiffuse_Toon( const in vec3 irradiance, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in ToonMaterial material, inout ReflectedLight reflectedLight ) {
	reflectedLight.indirectDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
#define RE_Direct				RE_Direct_Toon
#define RE_IndirectDiffuse		RE_IndirectDiffuse_Toon`,Yu=`BlinnPhongMaterial material;
material.diffuseColor = diffuseColor.rgb;
material.specularColor = specular;
material.specularShininess = shininess;
material.specularStrength = specularStrength;`,ju=`varying vec3 vViewPosition;
struct BlinnPhongMaterial {
	vec3 diffuseColor;
	vec3 specularColor;
	float specularShininess;
	float specularStrength;
};
void RE_Direct_BlinnPhong( const in IncidentLight directLight, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in BlinnPhongMaterial material, inout ReflectedLight reflectedLight ) {
	float dotNL = saturate( dot( geometryNormal, directLight.direction ) );
	vec3 irradiance = dotNL * directLight.color;
	reflectedLight.directDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
	reflectedLight.directSpecular += irradiance * BRDF_BlinnPhong( directLight.direction, geometryViewDir, geometryNormal, material.specularColor, material.specularShininess ) * material.specularStrength;
}
void RE_IndirectDiffuse_BlinnPhong( const in vec3 irradiance, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in BlinnPhongMaterial material, inout ReflectedLight reflectedLight ) {
	reflectedLight.indirectDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
#define RE_Direct				RE_Direct_BlinnPhong
#define RE_IndirectDiffuse		RE_IndirectDiffuse_BlinnPhong`,Ku=`PhysicalMaterial material;
material.diffuseColor = diffuseColor.rgb * ( 1.0 - metalnessFactor );
vec3 dxy = max( abs( dFdx( nonPerturbedNormal ) ), abs( dFdy( nonPerturbedNormal ) ) );
float geometryRoughness = max( max( dxy.x, dxy.y ), dxy.z );
material.roughness = max( roughnessFactor, 0.0525 );material.roughness += geometryRoughness;
material.roughness = min( material.roughness, 1.0 );
#ifdef IOR
	material.ior = ior;
	#ifdef USE_SPECULAR
		float specularIntensityFactor = specularIntensity;
		vec3 specularColorFactor = specularColor;
		#ifdef USE_SPECULAR_COLORMAP
			specularColorFactor *= texture2D( specularColorMap, vSpecularColorMapUv ).rgb;
		#endif
		#ifdef USE_SPECULAR_INTENSITYMAP
			specularIntensityFactor *= texture2D( specularIntensityMap, vSpecularIntensityMapUv ).a;
		#endif
		material.specularF90 = mix( specularIntensityFactor, 1.0, metalnessFactor );
	#else
		float specularIntensityFactor = 1.0;
		vec3 specularColorFactor = vec3( 1.0 );
		material.specularF90 = 1.0;
	#endif
	material.specularColor = mix( min( pow2( ( material.ior - 1.0 ) / ( material.ior + 1.0 ) ) * specularColorFactor, vec3( 1.0 ) ) * specularIntensityFactor, diffuseColor.rgb, metalnessFactor );
#else
	material.specularColor = mix( vec3( 0.04 ), diffuseColor.rgb, metalnessFactor );
	material.specularF90 = 1.0;
#endif
#ifdef USE_CLEARCOAT
	material.clearcoat = clearcoat;
	material.clearcoatRoughness = clearcoatRoughness;
	material.clearcoatF0 = vec3( 0.04 );
	material.clearcoatF90 = 1.0;
	#ifdef USE_CLEARCOATMAP
		material.clearcoat *= texture2D( clearcoatMap, vClearcoatMapUv ).x;
	#endif
	#ifdef USE_CLEARCOAT_ROUGHNESSMAP
		material.clearcoatRoughness *= texture2D( clearcoatRoughnessMap, vClearcoatRoughnessMapUv ).y;
	#endif
	material.clearcoat = saturate( material.clearcoat );	material.clearcoatRoughness = max( material.clearcoatRoughness, 0.0525 );
	material.clearcoatRoughness += geometryRoughness;
	material.clearcoatRoughness = min( material.clearcoatRoughness, 1.0 );
#endif
#ifdef USE_DISPERSION
	material.dispersion = dispersion;
#endif
#ifdef USE_IRIDESCENCE
	material.iridescence = iridescence;
	material.iridescenceIOR = iridescenceIOR;
	#ifdef USE_IRIDESCENCEMAP
		material.iridescence *= texture2D( iridescenceMap, vIridescenceMapUv ).r;
	#endif
	#ifdef USE_IRIDESCENCE_THICKNESSMAP
		material.iridescenceThickness = (iridescenceThicknessMaximum - iridescenceThicknessMinimum) * texture2D( iridescenceThicknessMap, vIridescenceThicknessMapUv ).g + iridescenceThicknessMinimum;
	#else
		material.iridescenceThickness = iridescenceThicknessMaximum;
	#endif
#endif
#ifdef USE_SHEEN
	material.sheenColor = sheenColor;
	#ifdef USE_SHEEN_COLORMAP
		material.sheenColor *= texture2D( sheenColorMap, vSheenColorMapUv ).rgb;
	#endif
	material.sheenRoughness = clamp( sheenRoughness, 0.07, 1.0 );
	#ifdef USE_SHEEN_ROUGHNESSMAP
		material.sheenRoughness *= texture2D( sheenRoughnessMap, vSheenRoughnessMapUv ).a;
	#endif
#endif
#ifdef USE_ANISOTROPY
	#ifdef USE_ANISOTROPYMAP
		mat2 anisotropyMat = mat2( anisotropyVector.x, anisotropyVector.y, - anisotropyVector.y, anisotropyVector.x );
		vec3 anisotropyPolar = texture2D( anisotropyMap, vAnisotropyMapUv ).rgb;
		vec2 anisotropyV = anisotropyMat * normalize( 2.0 * anisotropyPolar.rg - vec2( 1.0 ) ) * anisotropyPolar.b;
	#else
		vec2 anisotropyV = anisotropyVector;
	#endif
	material.anisotropy = length( anisotropyV );
	if( material.anisotropy == 0.0 ) {
		anisotropyV = vec2( 1.0, 0.0 );
	} else {
		anisotropyV /= material.anisotropy;
		material.anisotropy = saturate( material.anisotropy );
	}
	material.alphaT = mix( pow2( material.roughness ), 1.0, pow2( material.anisotropy ) );
	material.anisotropyT = tbn[ 0 ] * anisotropyV.x + tbn[ 1 ] * anisotropyV.y;
	material.anisotropyB = tbn[ 1 ] * anisotropyV.x - tbn[ 0 ] * anisotropyV.y;
#endif`,Zu=`struct PhysicalMaterial {
	vec3 diffuseColor;
	float roughness;
	vec3 specularColor;
	float specularF90;
	float dispersion;
	#ifdef USE_CLEARCOAT
		float clearcoat;
		float clearcoatRoughness;
		vec3 clearcoatF0;
		float clearcoatF90;
	#endif
	#ifdef USE_IRIDESCENCE
		float iridescence;
		float iridescenceIOR;
		float iridescenceThickness;
		vec3 iridescenceFresnel;
		vec3 iridescenceF0;
	#endif
	#ifdef USE_SHEEN
		vec3 sheenColor;
		float sheenRoughness;
	#endif
	#ifdef IOR
		float ior;
	#endif
	#ifdef USE_TRANSMISSION
		float transmission;
		float transmissionAlpha;
		float thickness;
		float attenuationDistance;
		vec3 attenuationColor;
	#endif
	#ifdef USE_ANISOTROPY
		float anisotropy;
		float alphaT;
		vec3 anisotropyT;
		vec3 anisotropyB;
	#endif
};
vec3 clearcoatSpecularDirect = vec3( 0.0 );
vec3 clearcoatSpecularIndirect = vec3( 0.0 );
vec3 sheenSpecularDirect = vec3( 0.0 );
vec3 sheenSpecularIndirect = vec3(0.0 );
vec3 Schlick_to_F0( const in vec3 f, const in float f90, const in float dotVH ) {
    float x = clamp( 1.0 - dotVH, 0.0, 1.0 );
    float x2 = x * x;
    float x5 = clamp( x * x2 * x2, 0.0, 0.9999 );
    return ( f - vec3( f90 ) * x5 ) / ( 1.0 - x5 );
}
float V_GGX_SmithCorrelated( const in float alpha, const in float dotNL, const in float dotNV ) {
	float a2 = pow2( alpha );
	float gv = dotNL * sqrt( a2 + ( 1.0 - a2 ) * pow2( dotNV ) );
	float gl = dotNV * sqrt( a2 + ( 1.0 - a2 ) * pow2( dotNL ) );
	return 0.5 / max( gv + gl, EPSILON );
}
float D_GGX( const in float alpha, const in float dotNH ) {
	float a2 = pow2( alpha );
	float denom = pow2( dotNH ) * ( a2 - 1.0 ) + 1.0;
	return RECIPROCAL_PI * a2 / pow2( denom );
}
#ifdef USE_ANISOTROPY
	float V_GGX_SmithCorrelated_Anisotropic( const in float alphaT, const in float alphaB, const in float dotTV, const in float dotBV, const in float dotTL, const in float dotBL, const in float dotNV, const in float dotNL ) {
		float gv = dotNL * length( vec3( alphaT * dotTV, alphaB * dotBV, dotNV ) );
		float gl = dotNV * length( vec3( alphaT * dotTL, alphaB * dotBL, dotNL ) );
		float v = 0.5 / ( gv + gl );
		return saturate(v);
	}
	float D_GGX_Anisotropic( const in float alphaT, const in float alphaB, const in float dotNH, const in float dotTH, const in float dotBH ) {
		float a2 = alphaT * alphaB;
		highp vec3 v = vec3( alphaB * dotTH, alphaT * dotBH, a2 * dotNH );
		highp float v2 = dot( v, v );
		float w2 = a2 / v2;
		return RECIPROCAL_PI * a2 * pow2 ( w2 );
	}
#endif
#ifdef USE_CLEARCOAT
	vec3 BRDF_GGX_Clearcoat( const in vec3 lightDir, const in vec3 viewDir, const in vec3 normal, const in PhysicalMaterial material) {
		vec3 f0 = material.clearcoatF0;
		float f90 = material.clearcoatF90;
		float roughness = material.clearcoatRoughness;
		float alpha = pow2( roughness );
		vec3 halfDir = normalize( lightDir + viewDir );
		float dotNL = saturate( dot( normal, lightDir ) );
		float dotNV = saturate( dot( normal, viewDir ) );
		float dotNH = saturate( dot( normal, halfDir ) );
		float dotVH = saturate( dot( viewDir, halfDir ) );
		vec3 F = F_Schlick( f0, f90, dotVH );
		float V = V_GGX_SmithCorrelated( alpha, dotNL, dotNV );
		float D = D_GGX( alpha, dotNH );
		return F * ( V * D );
	}
#endif
vec3 BRDF_GGX( const in vec3 lightDir, const in vec3 viewDir, const in vec3 normal, const in PhysicalMaterial material ) {
	vec3 f0 = material.specularColor;
	float f90 = material.specularF90;
	float roughness = material.roughness;
	float alpha = pow2( roughness );
	vec3 halfDir = normalize( lightDir + viewDir );
	float dotNL = saturate( dot( normal, lightDir ) );
	float dotNV = saturate( dot( normal, viewDir ) );
	float dotNH = saturate( dot( normal, halfDir ) );
	float dotVH = saturate( dot( viewDir, halfDir ) );
	vec3 F = F_Schlick( f0, f90, dotVH );
	#ifdef USE_IRIDESCENCE
		F = mix( F, material.iridescenceFresnel, material.iridescence );
	#endif
	#ifdef USE_ANISOTROPY
		float dotTL = dot( material.anisotropyT, lightDir );
		float dotTV = dot( material.anisotropyT, viewDir );
		float dotTH = dot( material.anisotropyT, halfDir );
		float dotBL = dot( material.anisotropyB, lightDir );
		float dotBV = dot( material.anisotropyB, viewDir );
		float dotBH = dot( material.anisotropyB, halfDir );
		float V = V_GGX_SmithCorrelated_Anisotropic( material.alphaT, alpha, dotTV, dotBV, dotTL, dotBL, dotNV, dotNL );
		float D = D_GGX_Anisotropic( material.alphaT, alpha, dotNH, dotTH, dotBH );
	#else
		float V = V_GGX_SmithCorrelated( alpha, dotNL, dotNV );
		float D = D_GGX( alpha, dotNH );
	#endif
	return F * ( V * D );
}
vec2 LTC_Uv( const in vec3 N, const in vec3 V, const in float roughness ) {
	const float LUT_SIZE = 64.0;
	const float LUT_SCALE = ( LUT_SIZE - 1.0 ) / LUT_SIZE;
	const float LUT_BIAS = 0.5 / LUT_SIZE;
	float dotNV = saturate( dot( N, V ) );
	vec2 uv = vec2( roughness, sqrt( 1.0 - dotNV ) );
	uv = uv * LUT_SCALE + LUT_BIAS;
	return uv;
}
float LTC_ClippedSphereFormFactor( const in vec3 f ) {
	float l = length( f );
	return max( ( l * l + f.z ) / ( l + 1.0 ), 0.0 );
}
vec3 LTC_EdgeVectorFormFactor( const in vec3 v1, const in vec3 v2 ) {
	float x = dot( v1, v2 );
	float y = abs( x );
	float a = 0.8543985 + ( 0.4965155 + 0.0145206 * y ) * y;
	float b = 3.4175940 + ( 4.1616724 + y ) * y;
	float v = a / b;
	float theta_sintheta = ( x > 0.0 ) ? v : 0.5 * inversesqrt( max( 1.0 - x * x, 1e-7 ) ) - v;
	return cross( v1, v2 ) * theta_sintheta;
}
vec3 LTC_Evaluate( const in vec3 N, const in vec3 V, const in vec3 P, const in mat3 mInv, const in vec3 rectCoords[ 4 ] ) {
	vec3 v1 = rectCoords[ 1 ] - rectCoords[ 0 ];
	vec3 v2 = rectCoords[ 3 ] - rectCoords[ 0 ];
	vec3 lightNormal = cross( v1, v2 );
	if( dot( lightNormal, P - rectCoords[ 0 ] ) < 0.0 ) return vec3( 0.0 );
	vec3 T1, T2;
	T1 = normalize( V - N * dot( V, N ) );
	T2 = - cross( N, T1 );
	mat3 mat = mInv * transposeMat3( mat3( T1, T2, N ) );
	vec3 coords[ 4 ];
	coords[ 0 ] = mat * ( rectCoords[ 0 ] - P );
	coords[ 1 ] = mat * ( rectCoords[ 1 ] - P );
	coords[ 2 ] = mat * ( rectCoords[ 2 ] - P );
	coords[ 3 ] = mat * ( rectCoords[ 3 ] - P );
	coords[ 0 ] = normalize( coords[ 0 ] );
	coords[ 1 ] = normalize( coords[ 1 ] );
	coords[ 2 ] = normalize( coords[ 2 ] );
	coords[ 3 ] = normalize( coords[ 3 ] );
	vec3 vectorFormFactor = vec3( 0.0 );
	vectorFormFactor += LTC_EdgeVectorFormFactor( coords[ 0 ], coords[ 1 ] );
	vectorFormFactor += LTC_EdgeVectorFormFactor( coords[ 1 ], coords[ 2 ] );
	vectorFormFactor += LTC_EdgeVectorFormFactor( coords[ 2 ], coords[ 3 ] );
	vectorFormFactor += LTC_EdgeVectorFormFactor( coords[ 3 ], coords[ 0 ] );
	float result = LTC_ClippedSphereFormFactor( vectorFormFactor );
	return vec3( result );
}
#if defined( USE_SHEEN )
float D_Charlie( float roughness, float dotNH ) {
	float alpha = pow2( roughness );
	float invAlpha = 1.0 / alpha;
	float cos2h = dotNH * dotNH;
	float sin2h = max( 1.0 - cos2h, 0.0078125 );
	return ( 2.0 + invAlpha ) * pow( sin2h, invAlpha * 0.5 ) / ( 2.0 * PI );
}
float V_Neubelt( float dotNV, float dotNL ) {
	return saturate( 1.0 / ( 4.0 * ( dotNL + dotNV - dotNL * dotNV ) ) );
}
vec3 BRDF_Sheen( const in vec3 lightDir, const in vec3 viewDir, const in vec3 normal, vec3 sheenColor, const in float sheenRoughness ) {
	vec3 halfDir = normalize( lightDir + viewDir );
	float dotNL = saturate( dot( normal, lightDir ) );
	float dotNV = saturate( dot( normal, viewDir ) );
	float dotNH = saturate( dot( normal, halfDir ) );
	float D = D_Charlie( sheenRoughness, dotNH );
	float V = V_Neubelt( dotNV, dotNL );
	return sheenColor * ( D * V );
}
#endif
float IBLSheenBRDF( const in vec3 normal, const in vec3 viewDir, const in float roughness ) {
	float dotNV = saturate( dot( normal, viewDir ) );
	float r2 = roughness * roughness;
	float a = roughness < 0.25 ? -339.2 * r2 + 161.4 * roughness - 25.9 : -8.48 * r2 + 14.3 * roughness - 9.95;
	float b = roughness < 0.25 ? 44.0 * r2 - 23.7 * roughness + 3.26 : 1.97 * r2 - 3.27 * roughness + 0.72;
	float DG = exp( a * dotNV + b ) + ( roughness < 0.25 ? 0.0 : 0.1 * ( roughness - 0.25 ) );
	return saturate( DG * RECIPROCAL_PI );
}
vec2 DFGApprox( const in vec3 normal, const in vec3 viewDir, const in float roughness ) {
	float dotNV = saturate( dot( normal, viewDir ) );
	const vec4 c0 = vec4( - 1, - 0.0275, - 0.572, 0.022 );
	const vec4 c1 = vec4( 1, 0.0425, 1.04, - 0.04 );
	vec4 r = roughness * c0 + c1;
	float a004 = min( r.x * r.x, exp2( - 9.28 * dotNV ) ) * r.x + r.y;
	vec2 fab = vec2( - 1.04, 1.04 ) * a004 + r.zw;
	return fab;
}
vec3 EnvironmentBRDF( const in vec3 normal, const in vec3 viewDir, const in vec3 specularColor, const in float specularF90, const in float roughness ) {
	vec2 fab = DFGApprox( normal, viewDir, roughness );
	return specularColor * fab.x + specularF90 * fab.y;
}
#ifdef USE_IRIDESCENCE
void computeMultiscatteringIridescence( const in vec3 normal, const in vec3 viewDir, const in vec3 specularColor, const in float specularF90, const in float iridescence, const in vec3 iridescenceF0, const in float roughness, inout vec3 singleScatter, inout vec3 multiScatter ) {
#else
void computeMultiscattering( const in vec3 normal, const in vec3 viewDir, const in vec3 specularColor, const in float specularF90, const in float roughness, inout vec3 singleScatter, inout vec3 multiScatter ) {
#endif
	vec2 fab = DFGApprox( normal, viewDir, roughness );
	#ifdef USE_IRIDESCENCE
		vec3 Fr = mix( specularColor, iridescenceF0, iridescence );
	#else
		vec3 Fr = specularColor;
	#endif
	vec3 FssEss = Fr * fab.x + specularF90 * fab.y;
	float Ess = fab.x + fab.y;
	float Ems = 1.0 - Ess;
	vec3 Favg = Fr + ( 1.0 - Fr ) * 0.047619;	vec3 Fms = FssEss * Favg / ( 1.0 - Ems * Favg );
	singleScatter += FssEss;
	multiScatter += Fms * Ems;
}
#if NUM_RECT_AREA_LIGHTS > 0
	void RE_Direct_RectArea_Physical( const in RectAreaLight rectAreaLight, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in PhysicalMaterial material, inout ReflectedLight reflectedLight ) {
		vec3 normal = geometryNormal;
		vec3 viewDir = geometryViewDir;
		vec3 position = geometryPosition;
		vec3 lightPos = rectAreaLight.position;
		vec3 halfWidth = rectAreaLight.halfWidth;
		vec3 halfHeight = rectAreaLight.halfHeight;
		vec3 lightColor = rectAreaLight.color;
		float roughness = material.roughness;
		vec3 rectCoords[ 4 ];
		rectCoords[ 0 ] = lightPos + halfWidth - halfHeight;		rectCoords[ 1 ] = lightPos - halfWidth - halfHeight;
		rectCoords[ 2 ] = lightPos - halfWidth + halfHeight;
		rectCoords[ 3 ] = lightPos + halfWidth + halfHeight;
		vec2 uv = LTC_Uv( normal, viewDir, roughness );
		vec4 t1 = texture2D( ltc_1, uv );
		vec4 t2 = texture2D( ltc_2, uv );
		mat3 mInv = mat3(
			vec3( t1.x, 0, t1.y ),
			vec3(    0, 1,    0 ),
			vec3( t1.z, 0, t1.w )
		);
		vec3 fresnel = ( material.specularColor * t2.x + ( vec3( 1.0 ) - material.specularColor ) * t2.y );
		reflectedLight.directSpecular += lightColor * fresnel * LTC_Evaluate( normal, viewDir, position, mInv, rectCoords );
		reflectedLight.directDiffuse += lightColor * material.diffuseColor * LTC_Evaluate( normal, viewDir, position, mat3( 1.0 ), rectCoords );
	}
#endif
void RE_Direct_Physical( const in IncidentLight directLight, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in PhysicalMaterial material, inout ReflectedLight reflectedLight ) {
	float dotNL = saturate( dot( geometryNormal, directLight.direction ) );
	vec3 irradiance = dotNL * directLight.color;
	#ifdef USE_CLEARCOAT
		float dotNLcc = saturate( dot( geometryClearcoatNormal, directLight.direction ) );
		vec3 ccIrradiance = dotNLcc * directLight.color;
		clearcoatSpecularDirect += ccIrradiance * BRDF_GGX_Clearcoat( directLight.direction, geometryViewDir, geometryClearcoatNormal, material );
	#endif
	#ifdef USE_SHEEN
		sheenSpecularDirect += irradiance * BRDF_Sheen( directLight.direction, geometryViewDir, geometryNormal, material.sheenColor, material.sheenRoughness );
	#endif
	reflectedLight.directSpecular += irradiance * BRDF_GGX( directLight.direction, geometryViewDir, geometryNormal, material );
	reflectedLight.directDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
void RE_IndirectDiffuse_Physical( const in vec3 irradiance, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in PhysicalMaterial material, inout ReflectedLight reflectedLight ) {
	reflectedLight.indirectDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
void RE_IndirectSpecular_Physical( const in vec3 radiance, const in vec3 irradiance, const in vec3 clearcoatRadiance, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in PhysicalMaterial material, inout ReflectedLight reflectedLight) {
	#ifdef USE_CLEARCOAT
		clearcoatSpecularIndirect += clearcoatRadiance * EnvironmentBRDF( geometryClearcoatNormal, geometryViewDir, material.clearcoatF0, material.clearcoatF90, material.clearcoatRoughness );
	#endif
	#ifdef USE_SHEEN
		sheenSpecularIndirect += irradiance * material.sheenColor * IBLSheenBRDF( geometryNormal, geometryViewDir, material.sheenRoughness );
	#endif
	vec3 singleScattering = vec3( 0.0 );
	vec3 multiScattering = vec3( 0.0 );
	vec3 cosineWeightedIrradiance = irradiance * RECIPROCAL_PI;
	#ifdef USE_IRIDESCENCE
		computeMultiscatteringIridescence( geometryNormal, geometryViewDir, material.specularColor, material.specularF90, material.iridescence, material.iridescenceFresnel, material.roughness, singleScattering, multiScattering );
	#else
		computeMultiscattering( geometryNormal, geometryViewDir, material.specularColor, material.specularF90, material.roughness, singleScattering, multiScattering );
	#endif
	vec3 totalScattering = singleScattering + multiScattering;
	vec3 diffuse = material.diffuseColor * ( 1.0 - max( max( totalScattering.r, totalScattering.g ), totalScattering.b ) );
	reflectedLight.indirectSpecular += radiance * singleScattering;
	reflectedLight.indirectSpecular += multiScattering * cosineWeightedIrradiance;
	reflectedLight.indirectDiffuse += diffuse * cosineWeightedIrradiance;
}
#define RE_Direct				RE_Direct_Physical
#define RE_Direct_RectArea		RE_Direct_RectArea_Physical
#define RE_IndirectDiffuse		RE_IndirectDiffuse_Physical
#define RE_IndirectSpecular		RE_IndirectSpecular_Physical
float computeSpecularOcclusion( const in float dotNV, const in float ambientOcclusion, const in float roughness ) {
	return saturate( pow( dotNV + ambientOcclusion, exp2( - 16.0 * roughness - 1.0 ) ) - 1.0 + ambientOcclusion );
}`,$u=`
vec3 geometryPosition = - vViewPosition;
vec3 geometryNormal = normal;
vec3 geometryViewDir = ( isOrthographic ) ? vec3( 0, 0, 1 ) : normalize( vViewPosition );
vec3 geometryClearcoatNormal = vec3( 0.0 );
#ifdef USE_CLEARCOAT
	geometryClearcoatNormal = clearcoatNormal;
#endif
#ifdef USE_IRIDESCENCE
	float dotNVi = saturate( dot( normal, geometryViewDir ) );
	if ( material.iridescenceThickness == 0.0 ) {
		material.iridescence = 0.0;
	} else {
		material.iridescence = saturate( material.iridescence );
	}
	if ( material.iridescence > 0.0 ) {
		material.iridescenceFresnel = evalIridescence( 1.0, material.iridescenceIOR, dotNVi, material.iridescenceThickness, material.specularColor );
		material.iridescenceF0 = Schlick_to_F0( material.iridescenceFresnel, 1.0, dotNVi );
	}
#endif
IncidentLight directLight;
#if ( NUM_POINT_LIGHTS > 0 ) && defined( RE_Direct )
	PointLight pointLight;
	#if defined( USE_SHADOWMAP ) && NUM_POINT_LIGHT_SHADOWS > 0
	PointLightShadow pointLightShadow;
	#endif
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_POINT_LIGHTS; i ++ ) {
		pointLight = pointLights[ i ];
		getPointLightInfo( pointLight, geometryPosition, directLight );
		#if defined( USE_SHADOWMAP ) && ( UNROLLED_LOOP_INDEX < NUM_POINT_LIGHT_SHADOWS )
		pointLightShadow = pointLightShadows[ i ];
		directLight.color *= ( directLight.visible && receiveShadow ) ? getPointShadow( pointShadowMap[ i ], pointLightShadow.shadowMapSize, pointLightShadow.shadowIntensity, pointLightShadow.shadowBias, pointLightShadow.shadowRadius, vPointShadowCoord[ i ], pointLightShadow.shadowCameraNear, pointLightShadow.shadowCameraFar ) : 1.0;
		#endif
		RE_Direct( directLight, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
	}
	#pragma unroll_loop_end
#endif
#if ( NUM_SPOT_LIGHTS > 0 ) && defined( RE_Direct )
	SpotLight spotLight;
	vec4 spotColor;
	vec3 spotLightCoord;
	bool inSpotLightMap;
	#if defined( USE_SHADOWMAP ) && NUM_SPOT_LIGHT_SHADOWS > 0
	SpotLightShadow spotLightShadow;
	#endif
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_SPOT_LIGHTS; i ++ ) {
		spotLight = spotLights[ i ];
		getSpotLightInfo( spotLight, geometryPosition, directLight );
		#if ( UNROLLED_LOOP_INDEX < NUM_SPOT_LIGHT_SHADOWS_WITH_MAPS )
		#define SPOT_LIGHT_MAP_INDEX UNROLLED_LOOP_INDEX
		#elif ( UNROLLED_LOOP_INDEX < NUM_SPOT_LIGHT_SHADOWS )
		#define SPOT_LIGHT_MAP_INDEX NUM_SPOT_LIGHT_MAPS
		#else
		#define SPOT_LIGHT_MAP_INDEX ( UNROLLED_LOOP_INDEX - NUM_SPOT_LIGHT_SHADOWS + NUM_SPOT_LIGHT_SHADOWS_WITH_MAPS )
		#endif
		#if ( SPOT_LIGHT_MAP_INDEX < NUM_SPOT_LIGHT_MAPS )
			spotLightCoord = vSpotLightCoord[ i ].xyz / vSpotLightCoord[ i ].w;
			inSpotLightMap = all( lessThan( abs( spotLightCoord * 2. - 1. ), vec3( 1.0 ) ) );
			spotColor = texture2D( spotLightMap[ SPOT_LIGHT_MAP_INDEX ], spotLightCoord.xy );
			directLight.color = inSpotLightMap ? directLight.color * spotColor.rgb : directLight.color;
		#endif
		#undef SPOT_LIGHT_MAP_INDEX
		#if defined( USE_SHADOWMAP ) && ( UNROLLED_LOOP_INDEX < NUM_SPOT_LIGHT_SHADOWS )
		spotLightShadow = spotLightShadows[ i ];
		directLight.color *= ( directLight.visible && receiveShadow ) ? getShadow( spotShadowMap[ i ], spotLightShadow.shadowMapSize, spotLightShadow.shadowIntensity, spotLightShadow.shadowBias, spotLightShadow.shadowRadius, vSpotLightCoord[ i ] ) : 1.0;
		#endif
		RE_Direct( directLight, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
	}
	#pragma unroll_loop_end
#endif
#if ( NUM_DIR_LIGHTS > 0 ) && defined( RE_Direct )
	DirectionalLight directionalLight;
	#if defined( USE_SHADOWMAP ) && NUM_DIR_LIGHT_SHADOWS > 0
	DirectionalLightShadow directionalLightShadow;
	#endif
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_DIR_LIGHTS; i ++ ) {
		directionalLight = directionalLights[ i ];
		getDirectionalLightInfo( directionalLight, directLight );
		#if defined( USE_SHADOWMAP ) && ( UNROLLED_LOOP_INDEX < NUM_DIR_LIGHT_SHADOWS )
		directionalLightShadow = directionalLightShadows[ i ];
		directLight.color *= ( directLight.visible && receiveShadow ) ? getShadow( directionalShadowMap[ i ], directionalLightShadow.shadowMapSize, directionalLightShadow.shadowIntensity, directionalLightShadow.shadowBias, directionalLightShadow.shadowRadius, vDirectionalShadowCoord[ i ] ) : 1.0;
		#endif
		RE_Direct( directLight, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
	}
	#pragma unroll_loop_end
#endif
#if ( NUM_RECT_AREA_LIGHTS > 0 ) && defined( RE_Direct_RectArea )
	RectAreaLight rectAreaLight;
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_RECT_AREA_LIGHTS; i ++ ) {
		rectAreaLight = rectAreaLights[ i ];
		RE_Direct_RectArea( rectAreaLight, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
	}
	#pragma unroll_loop_end
#endif
#if defined( RE_IndirectDiffuse )
	vec3 iblIrradiance = vec3( 0.0 );
	vec3 irradiance = getAmbientLightIrradiance( ambientLightColor );
	#if defined( USE_LIGHT_PROBES )
		irradiance += getLightProbeIrradiance( lightProbe, geometryNormal );
	#endif
	#if ( NUM_HEMI_LIGHTS > 0 )
		#pragma unroll_loop_start
		for ( int i = 0; i < NUM_HEMI_LIGHTS; i ++ ) {
			irradiance += getHemisphereLightIrradiance( hemisphereLights[ i ], geometryNormal );
		}
		#pragma unroll_loop_end
	#endif
#endif
#if defined( RE_IndirectSpecular )
	vec3 radiance = vec3( 0.0 );
	vec3 clearcoatRadiance = vec3( 0.0 );
#endif`,Ju=`#if defined( RE_IndirectDiffuse )
	#ifdef USE_LIGHTMAP
		vec4 lightMapTexel = texture2D( lightMap, vLightMapUv );
		vec3 lightMapIrradiance = lightMapTexel.rgb * lightMapIntensity;
		irradiance += lightMapIrradiance;
	#endif
	#if defined( USE_ENVMAP ) && defined( STANDARD ) && defined( ENVMAP_TYPE_CUBE_UV )
		iblIrradiance += getIBLIrradiance( geometryNormal );
	#endif
#endif
#if defined( USE_ENVMAP ) && defined( RE_IndirectSpecular )
	#ifdef USE_ANISOTROPY
		radiance += getIBLAnisotropyRadiance( geometryViewDir, geometryNormal, material.roughness, material.anisotropyB, material.anisotropy );
	#else
		radiance += getIBLRadiance( geometryViewDir, geometryNormal, material.roughness );
	#endif
	#ifdef USE_CLEARCOAT
		clearcoatRadiance += getIBLRadiance( geometryViewDir, geometryClearcoatNormal, material.clearcoatRoughness );
	#endif
#endif`,Qu=`#if defined( RE_IndirectDiffuse )
	RE_IndirectDiffuse( irradiance, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
#endif
#if defined( RE_IndirectSpecular )
	RE_IndirectSpecular( radiance, iblIrradiance, clearcoatRadiance, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
#endif`,td=`#if defined( USE_LOGARITHMIC_DEPTH_BUFFER )
	gl_FragDepth = vIsPerspective == 0.0 ? gl_FragCoord.z : log2( vFragDepth ) * logDepthBufFC * 0.5;
#endif`,ed=`#if defined( USE_LOGARITHMIC_DEPTH_BUFFER )
	uniform float logDepthBufFC;
	varying float vFragDepth;
	varying float vIsPerspective;
#endif`,nd=`#ifdef USE_LOGARITHMIC_DEPTH_BUFFER
	varying float vFragDepth;
	varying float vIsPerspective;
#endif`,id=`#ifdef USE_LOGARITHMIC_DEPTH_BUFFER
	vFragDepth = 1.0 + gl_Position.w;
	vIsPerspective = float( isPerspectiveMatrix( projectionMatrix ) );
#endif`,sd=`#ifdef USE_MAP
	vec4 sampledDiffuseColor = texture2D( map, vMapUv );
	#ifdef DECODE_VIDEO_TEXTURE
		sampledDiffuseColor = sRGBTransferEOTF( sampledDiffuseColor );
	#endif
	diffuseColor *= sampledDiffuseColor;
#endif`,rd=`#ifdef USE_MAP
	uniform sampler2D map;
#endif`,ad=`#if defined( USE_MAP ) || defined( USE_ALPHAMAP )
	#if defined( USE_POINTS_UV )
		vec2 uv = vUv;
	#else
		vec2 uv = ( uvTransform * vec3( gl_PointCoord.x, 1.0 - gl_PointCoord.y, 1 ) ).xy;
	#endif
#endif
#ifdef USE_MAP
	diffuseColor *= texture2D( map, uv );
#endif
#ifdef USE_ALPHAMAP
	diffuseColor.a *= texture2D( alphaMap, uv ).g;
#endif`,od=`#if defined( USE_POINTS_UV )
	varying vec2 vUv;
#else
	#if defined( USE_MAP ) || defined( USE_ALPHAMAP )
		uniform mat3 uvTransform;
	#endif
#endif
#ifdef USE_MAP
	uniform sampler2D map;
#endif
#ifdef USE_ALPHAMAP
	uniform sampler2D alphaMap;
#endif`,ld=`float metalnessFactor = metalness;
#ifdef USE_METALNESSMAP
	vec4 texelMetalness = texture2D( metalnessMap, vMetalnessMapUv );
	metalnessFactor *= texelMetalness.b;
#endif`,cd=`#ifdef USE_METALNESSMAP
	uniform sampler2D metalnessMap;
#endif`,hd=`#ifdef USE_INSTANCING_MORPH
	float morphTargetInfluences[ MORPHTARGETS_COUNT ];
	float morphTargetBaseInfluence = texelFetch( morphTexture, ivec2( 0, gl_InstanceID ), 0 ).r;
	for ( int i = 0; i < MORPHTARGETS_COUNT; i ++ ) {
		morphTargetInfluences[i] =  texelFetch( morphTexture, ivec2( i + 1, gl_InstanceID ), 0 ).r;
	}
#endif`,ud=`#if defined( USE_MORPHCOLORS )
	vColor *= morphTargetBaseInfluence;
	for ( int i = 0; i < MORPHTARGETS_COUNT; i ++ ) {
		#if defined( USE_COLOR_ALPHA )
			if ( morphTargetInfluences[ i ] != 0.0 ) vColor += getMorph( gl_VertexID, i, 2 ) * morphTargetInfluences[ i ];
		#elif defined( USE_COLOR )
			if ( morphTargetInfluences[ i ] != 0.0 ) vColor += getMorph( gl_VertexID, i, 2 ).rgb * morphTargetInfluences[ i ];
		#endif
	}
#endif`,dd=`#ifdef USE_MORPHNORMALS
	objectNormal *= morphTargetBaseInfluence;
	for ( int i = 0; i < MORPHTARGETS_COUNT; i ++ ) {
		if ( morphTargetInfluences[ i ] != 0.0 ) objectNormal += getMorph( gl_VertexID, i, 1 ).xyz * morphTargetInfluences[ i ];
	}
#endif`,fd=`#ifdef USE_MORPHTARGETS
	#ifndef USE_INSTANCING_MORPH
		uniform float morphTargetBaseInfluence;
		uniform float morphTargetInfluences[ MORPHTARGETS_COUNT ];
	#endif
	uniform sampler2DArray morphTargetsTexture;
	uniform ivec2 morphTargetsTextureSize;
	vec4 getMorph( const in int vertexIndex, const in int morphTargetIndex, const in int offset ) {
		int texelIndex = vertexIndex * MORPHTARGETS_TEXTURE_STRIDE + offset;
		int y = texelIndex / morphTargetsTextureSize.x;
		int x = texelIndex - y * morphTargetsTextureSize.x;
		ivec3 morphUV = ivec3( x, y, morphTargetIndex );
		return texelFetch( morphTargetsTexture, morphUV, 0 );
	}
#endif`,pd=`#ifdef USE_MORPHTARGETS
	transformed *= morphTargetBaseInfluence;
	for ( int i = 0; i < MORPHTARGETS_COUNT; i ++ ) {
		if ( morphTargetInfluences[ i ] != 0.0 ) transformed += getMorph( gl_VertexID, i, 0 ).xyz * morphTargetInfluences[ i ];
	}
#endif`,md=`float faceDirection = gl_FrontFacing ? 1.0 : - 1.0;
#ifdef FLAT_SHADED
	vec3 fdx = dFdx( vViewPosition );
	vec3 fdy = dFdy( vViewPosition );
	vec3 normal = normalize( cross( fdx, fdy ) );
#else
	vec3 normal = normalize( vNormal );
	#ifdef DOUBLE_SIDED
		normal *= faceDirection;
	#endif
#endif
#if defined( USE_NORMALMAP_TANGENTSPACE ) || defined( USE_CLEARCOAT_NORMALMAP ) || defined( USE_ANISOTROPY )
	#ifdef USE_TANGENT
		mat3 tbn = mat3( normalize( vTangent ), normalize( vBitangent ), normal );
	#else
		mat3 tbn = getTangentFrame( - vViewPosition, normal,
		#if defined( USE_NORMALMAP )
			vNormalMapUv
		#elif defined( USE_CLEARCOAT_NORMALMAP )
			vClearcoatNormalMapUv
		#else
			vUv
		#endif
		);
	#endif
	#if defined( DOUBLE_SIDED ) && ! defined( FLAT_SHADED )
		tbn[0] *= faceDirection;
		tbn[1] *= faceDirection;
	#endif
#endif
#ifdef USE_CLEARCOAT_NORMALMAP
	#ifdef USE_TANGENT
		mat3 tbn2 = mat3( normalize( vTangent ), normalize( vBitangent ), normal );
	#else
		mat3 tbn2 = getTangentFrame( - vViewPosition, normal, vClearcoatNormalMapUv );
	#endif
	#if defined( DOUBLE_SIDED ) && ! defined( FLAT_SHADED )
		tbn2[0] *= faceDirection;
		tbn2[1] *= faceDirection;
	#endif
#endif
vec3 nonPerturbedNormal = normal;`,gd=`#ifdef USE_NORMALMAP_OBJECTSPACE
	normal = texture2D( normalMap, vNormalMapUv ).xyz * 2.0 - 1.0;
	#ifdef FLIP_SIDED
		normal = - normal;
	#endif
	#ifdef DOUBLE_SIDED
		normal = normal * faceDirection;
	#endif
	normal = normalize( normalMatrix * normal );
#elif defined( USE_NORMALMAP_TANGENTSPACE )
	vec3 mapN = texture2D( normalMap, vNormalMapUv ).xyz * 2.0 - 1.0;
	mapN.xy *= normalScale;
	normal = normalize( tbn * mapN );
#elif defined( USE_BUMPMAP )
	normal = perturbNormalArb( - vViewPosition, normal, dHdxy_fwd(), faceDirection );
#endif`,_d=`#ifndef FLAT_SHADED
	varying vec3 vNormal;
	#ifdef USE_TANGENT
		varying vec3 vTangent;
		varying vec3 vBitangent;
	#endif
#endif`,xd=`#ifndef FLAT_SHADED
	varying vec3 vNormal;
	#ifdef USE_TANGENT
		varying vec3 vTangent;
		varying vec3 vBitangent;
	#endif
#endif`,vd=`#ifndef FLAT_SHADED
	vNormal = normalize( transformedNormal );
	#ifdef USE_TANGENT
		vTangent = normalize( transformedTangent );
		vBitangent = normalize( cross( vNormal, vTangent ) * tangent.w );
	#endif
#endif`,yd=`#ifdef USE_NORMALMAP
	uniform sampler2D normalMap;
	uniform vec2 normalScale;
#endif
#ifdef USE_NORMALMAP_OBJECTSPACE
	uniform mat3 normalMatrix;
#endif
#if ! defined ( USE_TANGENT ) && ( defined ( USE_NORMALMAP_TANGENTSPACE ) || defined ( USE_CLEARCOAT_NORMALMAP ) || defined( USE_ANISOTROPY ) )
	mat3 getTangentFrame( vec3 eye_pos, vec3 surf_norm, vec2 uv ) {
		vec3 q0 = dFdx( eye_pos.xyz );
		vec3 q1 = dFdy( eye_pos.xyz );
		vec2 st0 = dFdx( uv.st );
		vec2 st1 = dFdy( uv.st );
		vec3 N = surf_norm;
		vec3 q1perp = cross( q1, N );
		vec3 q0perp = cross( N, q0 );
		vec3 T = q1perp * st0.x + q0perp * st1.x;
		vec3 B = q1perp * st0.y + q0perp * st1.y;
		float det = max( dot( T, T ), dot( B, B ) );
		float scale = ( det == 0.0 ) ? 0.0 : inversesqrt( det );
		return mat3( T * scale, B * scale, N );
	}
#endif`,Md=`#ifdef USE_CLEARCOAT
	vec3 clearcoatNormal = nonPerturbedNormal;
#endif`,Sd=`#ifdef USE_CLEARCOAT_NORMALMAP
	vec3 clearcoatMapN = texture2D( clearcoatNormalMap, vClearcoatNormalMapUv ).xyz * 2.0 - 1.0;
	clearcoatMapN.xy *= clearcoatNormalScale;
	clearcoatNormal = normalize( tbn2 * clearcoatMapN );
#endif`,Ed=`#ifdef USE_CLEARCOATMAP
	uniform sampler2D clearcoatMap;
#endif
#ifdef USE_CLEARCOAT_NORMALMAP
	uniform sampler2D clearcoatNormalMap;
	uniform vec2 clearcoatNormalScale;
#endif
#ifdef USE_CLEARCOAT_ROUGHNESSMAP
	uniform sampler2D clearcoatRoughnessMap;
#endif`,bd=`#ifdef USE_IRIDESCENCEMAP
	uniform sampler2D iridescenceMap;
#endif
#ifdef USE_IRIDESCENCE_THICKNESSMAP
	uniform sampler2D iridescenceThicknessMap;
#endif`,Td=`#ifdef OPAQUE
diffuseColor.a = 1.0;
#endif
#ifdef USE_TRANSMISSION
diffuseColor.a *= material.transmissionAlpha;
#endif
gl_FragColor = vec4( outgoingLight, diffuseColor.a );`,wd=`vec3 packNormalToRGB( const in vec3 normal ) {
	return normalize( normal ) * 0.5 + 0.5;
}
vec3 unpackRGBToNormal( const in vec3 rgb ) {
	return 2.0 * rgb.xyz - 1.0;
}
const float PackUpscale = 256. / 255.;const float UnpackDownscale = 255. / 256.;const float ShiftRight8 = 1. / 256.;
const float Inv255 = 1. / 255.;
const vec4 PackFactors = vec4( 1.0, 256.0, 256.0 * 256.0, 256.0 * 256.0 * 256.0 );
const vec2 UnpackFactors2 = vec2( UnpackDownscale, 1.0 / PackFactors.g );
const vec3 UnpackFactors3 = vec3( UnpackDownscale / PackFactors.rg, 1.0 / PackFactors.b );
const vec4 UnpackFactors4 = vec4( UnpackDownscale / PackFactors.rgb, 1.0 / PackFactors.a );
vec4 packDepthToRGBA( const in float v ) {
	if( v <= 0.0 )
		return vec4( 0., 0., 0., 0. );
	if( v >= 1.0 )
		return vec4( 1., 1., 1., 1. );
	float vuf;
	float af = modf( v * PackFactors.a, vuf );
	float bf = modf( vuf * ShiftRight8, vuf );
	float gf = modf( vuf * ShiftRight8, vuf );
	return vec4( vuf * Inv255, gf * PackUpscale, bf * PackUpscale, af );
}
vec3 packDepthToRGB( const in float v ) {
	if( v <= 0.0 )
		return vec3( 0., 0., 0. );
	if( v >= 1.0 )
		return vec3( 1., 1., 1. );
	float vuf;
	float bf = modf( v * PackFactors.b, vuf );
	float gf = modf( vuf * ShiftRight8, vuf );
	return vec3( vuf * Inv255, gf * PackUpscale, bf );
}
vec2 packDepthToRG( const in float v ) {
	if( v <= 0.0 )
		return vec2( 0., 0. );
	if( v >= 1.0 )
		return vec2( 1., 1. );
	float vuf;
	float gf = modf( v * 256., vuf );
	return vec2( vuf * Inv255, gf );
}
float unpackRGBAToDepth( const in vec4 v ) {
	return dot( v, UnpackFactors4 );
}
float unpackRGBToDepth( const in vec3 v ) {
	return dot( v, UnpackFactors3 );
}
float unpackRGToDepth( const in vec2 v ) {
	return v.r * UnpackFactors2.r + v.g * UnpackFactors2.g;
}
vec4 pack2HalfToRGBA( const in vec2 v ) {
	vec4 r = vec4( v.x, fract( v.x * 255.0 ), v.y, fract( v.y * 255.0 ) );
	return vec4( r.x - r.y / 255.0, r.y, r.z - r.w / 255.0, r.w );
}
vec2 unpackRGBATo2Half( const in vec4 v ) {
	return vec2( v.x + ( v.y / 255.0 ), v.z + ( v.w / 255.0 ) );
}
float viewZToOrthographicDepth( const in float viewZ, const in float near, const in float far ) {
	return ( viewZ + near ) / ( near - far );
}
float orthographicDepthToViewZ( const in float depth, const in float near, const in float far ) {
	return depth * ( near - far ) - near;
}
float viewZToPerspectiveDepth( const in float viewZ, const in float near, const in float far ) {
	return ( ( near + viewZ ) * far ) / ( ( far - near ) * viewZ );
}
float perspectiveDepthToViewZ( const in float depth, const in float near, const in float far ) {
	return ( near * far ) / ( ( far - near ) * depth - far );
}`,Ad=`#ifdef PREMULTIPLIED_ALPHA
	gl_FragColor.rgb *= gl_FragColor.a;
#endif`,Cd=`vec4 mvPosition = vec4( transformed, 1.0 );
#ifdef USE_BATCHING
	mvPosition = batchingMatrix * mvPosition;
#endif
#ifdef USE_INSTANCING
	mvPosition = instanceMatrix * mvPosition;
#endif
mvPosition = modelViewMatrix * mvPosition;
gl_Position = projectionMatrix * mvPosition;`,Rd=`#ifdef DITHERING
	gl_FragColor.rgb = dithering( gl_FragColor.rgb );
#endif`,Pd=`#ifdef DITHERING
	vec3 dithering( vec3 color ) {
		float grid_position = rand( gl_FragCoord.xy );
		vec3 dither_shift_RGB = vec3( 0.25 / 255.0, -0.25 / 255.0, 0.25 / 255.0 );
		dither_shift_RGB = mix( 2.0 * dither_shift_RGB, -2.0 * dither_shift_RGB, grid_position );
		return color + dither_shift_RGB;
	}
#endif`,Ld=`float roughnessFactor = roughness;
#ifdef USE_ROUGHNESSMAP
	vec4 texelRoughness = texture2D( roughnessMap, vRoughnessMapUv );
	roughnessFactor *= texelRoughness.g;
#endif`,Dd=`#ifdef USE_ROUGHNESSMAP
	uniform sampler2D roughnessMap;
#endif`,Id=`#if NUM_SPOT_LIGHT_COORDS > 0
	varying vec4 vSpotLightCoord[ NUM_SPOT_LIGHT_COORDS ];
#endif
#if NUM_SPOT_LIGHT_MAPS > 0
	uniform sampler2D spotLightMap[ NUM_SPOT_LIGHT_MAPS ];
#endif
#ifdef USE_SHADOWMAP
	#if NUM_DIR_LIGHT_SHADOWS > 0
		uniform sampler2D directionalShadowMap[ NUM_DIR_LIGHT_SHADOWS ];
		varying vec4 vDirectionalShadowCoord[ NUM_DIR_LIGHT_SHADOWS ];
		struct DirectionalLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
		};
		uniform DirectionalLightShadow directionalLightShadows[ NUM_DIR_LIGHT_SHADOWS ];
	#endif
	#if NUM_SPOT_LIGHT_SHADOWS > 0
		uniform sampler2D spotShadowMap[ NUM_SPOT_LIGHT_SHADOWS ];
		struct SpotLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
		};
		uniform SpotLightShadow spotLightShadows[ NUM_SPOT_LIGHT_SHADOWS ];
	#endif
	#if NUM_POINT_LIGHT_SHADOWS > 0
		uniform sampler2D pointShadowMap[ NUM_POINT_LIGHT_SHADOWS ];
		varying vec4 vPointShadowCoord[ NUM_POINT_LIGHT_SHADOWS ];
		struct PointLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
			float shadowCameraNear;
			float shadowCameraFar;
		};
		uniform PointLightShadow pointLightShadows[ NUM_POINT_LIGHT_SHADOWS ];
	#endif
	float texture2DCompare( sampler2D depths, vec2 uv, float compare ) {
		float depth = unpackRGBAToDepth( texture2D( depths, uv ) );
		#ifdef USE_REVERSED_DEPTH_BUFFER
			return step( depth, compare );
		#else
			return step( compare, depth );
		#endif
	}
	vec2 texture2DDistribution( sampler2D shadow, vec2 uv ) {
		return unpackRGBATo2Half( texture2D( shadow, uv ) );
	}
	float VSMShadow( sampler2D shadow, vec2 uv, float compare ) {
		float occlusion = 1.0;
		vec2 distribution = texture2DDistribution( shadow, uv );
		#ifdef USE_REVERSED_DEPTH_BUFFER
			float hard_shadow = step( distribution.x, compare );
		#else
			float hard_shadow = step( compare, distribution.x );
		#endif
		if ( hard_shadow != 1.0 ) {
			float distance = compare - distribution.x;
			float variance = max( 0.00000, distribution.y * distribution.y );
			float softness_probability = variance / (variance + distance * distance );			softness_probability = clamp( ( softness_probability - 0.3 ) / ( 0.95 - 0.3 ), 0.0, 1.0 );			occlusion = clamp( max( hard_shadow, softness_probability ), 0.0, 1.0 );
		}
		return occlusion;
	}
	float getShadow( sampler2D shadowMap, vec2 shadowMapSize, float shadowIntensity, float shadowBias, float shadowRadius, vec4 shadowCoord ) {
		float shadow = 1.0;
		shadowCoord.xyz /= shadowCoord.w;
		shadowCoord.z += shadowBias;
		bool inFrustum = shadowCoord.x >= 0.0 && shadowCoord.x <= 1.0 && shadowCoord.y >= 0.0 && shadowCoord.y <= 1.0;
		bool frustumTest = inFrustum && shadowCoord.z <= 1.0;
		if ( frustumTest ) {
		#if defined( SHADOWMAP_TYPE_PCF )
			vec2 texelSize = vec2( 1.0 ) / shadowMapSize;
			float dx0 = - texelSize.x * shadowRadius;
			float dy0 = - texelSize.y * shadowRadius;
			float dx1 = + texelSize.x * shadowRadius;
			float dy1 = + texelSize.y * shadowRadius;
			float dx2 = dx0 / 2.0;
			float dy2 = dy0 / 2.0;
			float dx3 = dx1 / 2.0;
			float dy3 = dy1 / 2.0;
			shadow = (
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx0, dy0 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( 0.0, dy0 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx1, dy0 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx2, dy2 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( 0.0, dy2 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx3, dy2 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx0, 0.0 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx2, 0.0 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy, shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx3, 0.0 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx1, 0.0 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx2, dy3 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( 0.0, dy3 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx3, dy3 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx0, dy1 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( 0.0, dy1 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, shadowCoord.xy + vec2( dx1, dy1 ), shadowCoord.z )
			) * ( 1.0 / 17.0 );
		#elif defined( SHADOWMAP_TYPE_PCF_SOFT )
			vec2 texelSize = vec2( 1.0 ) / shadowMapSize;
			float dx = texelSize.x;
			float dy = texelSize.y;
			vec2 uv = shadowCoord.xy;
			vec2 f = fract( uv * shadowMapSize + 0.5 );
			uv -= f * texelSize;
			shadow = (
				texture2DCompare( shadowMap, uv, shadowCoord.z ) +
				texture2DCompare( shadowMap, uv + vec2( dx, 0.0 ), shadowCoord.z ) +
				texture2DCompare( shadowMap, uv + vec2( 0.0, dy ), shadowCoord.z ) +
				texture2DCompare( shadowMap, uv + texelSize, shadowCoord.z ) +
				mix( texture2DCompare( shadowMap, uv + vec2( -dx, 0.0 ), shadowCoord.z ),
					 texture2DCompare( shadowMap, uv + vec2( 2.0 * dx, 0.0 ), shadowCoord.z ),
					 f.x ) +
				mix( texture2DCompare( shadowMap, uv + vec2( -dx, dy ), shadowCoord.z ),
					 texture2DCompare( shadowMap, uv + vec2( 2.0 * dx, dy ), shadowCoord.z ),
					 f.x ) +
				mix( texture2DCompare( shadowMap, uv + vec2( 0.0, -dy ), shadowCoord.z ),
					 texture2DCompare( shadowMap, uv + vec2( 0.0, 2.0 * dy ), shadowCoord.z ),
					 f.y ) +
				mix( texture2DCompare( shadowMap, uv + vec2( dx, -dy ), shadowCoord.z ),
					 texture2DCompare( shadowMap, uv + vec2( dx, 2.0 * dy ), shadowCoord.z ),
					 f.y ) +
				mix( mix( texture2DCompare( shadowMap, uv + vec2( -dx, -dy ), shadowCoord.z ),
						  texture2DCompare( shadowMap, uv + vec2( 2.0 * dx, -dy ), shadowCoord.z ),
						  f.x ),
					 mix( texture2DCompare( shadowMap, uv + vec2( -dx, 2.0 * dy ), shadowCoord.z ),
						  texture2DCompare( shadowMap, uv + vec2( 2.0 * dx, 2.0 * dy ), shadowCoord.z ),
						  f.x ),
					 f.y )
			) * ( 1.0 / 9.0 );
		#elif defined( SHADOWMAP_TYPE_VSM )
			shadow = VSMShadow( shadowMap, shadowCoord.xy, shadowCoord.z );
		#else
			shadow = texture2DCompare( shadowMap, shadowCoord.xy, shadowCoord.z );
		#endif
		}
		return mix( 1.0, shadow, shadowIntensity );
	}
	vec2 cubeToUV( vec3 v, float texelSizeY ) {
		vec3 absV = abs( v );
		float scaleToCube = 1.0 / max( absV.x, max( absV.y, absV.z ) );
		absV *= scaleToCube;
		v *= scaleToCube * ( 1.0 - 2.0 * texelSizeY );
		vec2 planar = v.xy;
		float almostATexel = 1.5 * texelSizeY;
		float almostOne = 1.0 - almostATexel;
		if ( absV.z >= almostOne ) {
			if ( v.z > 0.0 )
				planar.x = 4.0 - v.x;
		} else if ( absV.x >= almostOne ) {
			float signX = sign( v.x );
			planar.x = v.z * signX + 2.0 * signX;
		} else if ( absV.y >= almostOne ) {
			float signY = sign( v.y );
			planar.x = v.x + 2.0 * signY + 2.0;
			planar.y = v.z * signY - 2.0;
		}
		return vec2( 0.125, 0.25 ) * planar + vec2( 0.375, 0.75 );
	}
	float getPointShadow( sampler2D shadowMap, vec2 shadowMapSize, float shadowIntensity, float shadowBias, float shadowRadius, vec4 shadowCoord, float shadowCameraNear, float shadowCameraFar ) {
		float shadow = 1.0;
		vec3 lightToPosition = shadowCoord.xyz;
		
		float lightToPositionLength = length( lightToPosition );
		if ( lightToPositionLength - shadowCameraFar <= 0.0 && lightToPositionLength - shadowCameraNear >= 0.0 ) {
			float dp = ( lightToPositionLength - shadowCameraNear ) / ( shadowCameraFar - shadowCameraNear );			dp += shadowBias;
			vec3 bd3D = normalize( lightToPosition );
			vec2 texelSize = vec2( 1.0 ) / ( shadowMapSize * vec2( 4.0, 2.0 ) );
			#if defined( SHADOWMAP_TYPE_PCF ) || defined( SHADOWMAP_TYPE_PCF_SOFT ) || defined( SHADOWMAP_TYPE_VSM )
				vec2 offset = vec2( - 1, 1 ) * shadowRadius * texelSize.y;
				shadow = (
					texture2DCompare( shadowMap, cubeToUV( bd3D + offset.xyy, texelSize.y ), dp ) +
					texture2DCompare( shadowMap, cubeToUV( bd3D + offset.yyy, texelSize.y ), dp ) +
					texture2DCompare( shadowMap, cubeToUV( bd3D + offset.xyx, texelSize.y ), dp ) +
					texture2DCompare( shadowMap, cubeToUV( bd3D + offset.yyx, texelSize.y ), dp ) +
					texture2DCompare( shadowMap, cubeToUV( bd3D, texelSize.y ), dp ) +
					texture2DCompare( shadowMap, cubeToUV( bd3D + offset.xxy, texelSize.y ), dp ) +
					texture2DCompare( shadowMap, cubeToUV( bd3D + offset.yxy, texelSize.y ), dp ) +
					texture2DCompare( shadowMap, cubeToUV( bd3D + offset.xxx, texelSize.y ), dp ) +
					texture2DCompare( shadowMap, cubeToUV( bd3D + offset.yxx, texelSize.y ), dp )
				) * ( 1.0 / 9.0 );
			#else
				shadow = texture2DCompare( shadowMap, cubeToUV( bd3D, texelSize.y ), dp );
			#endif
		}
		return mix( 1.0, shadow, shadowIntensity );
	}
#endif`,Ud=`#if NUM_SPOT_LIGHT_COORDS > 0
	uniform mat4 spotLightMatrix[ NUM_SPOT_LIGHT_COORDS ];
	varying vec4 vSpotLightCoord[ NUM_SPOT_LIGHT_COORDS ];
#endif
#ifdef USE_SHADOWMAP
	#if NUM_DIR_LIGHT_SHADOWS > 0
		uniform mat4 directionalShadowMatrix[ NUM_DIR_LIGHT_SHADOWS ];
		varying vec4 vDirectionalShadowCoord[ NUM_DIR_LIGHT_SHADOWS ];
		struct DirectionalLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
		};
		uniform DirectionalLightShadow directionalLightShadows[ NUM_DIR_LIGHT_SHADOWS ];
	#endif
	#if NUM_SPOT_LIGHT_SHADOWS > 0
		struct SpotLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
		};
		uniform SpotLightShadow spotLightShadows[ NUM_SPOT_LIGHT_SHADOWS ];
	#endif
	#if NUM_POINT_LIGHT_SHADOWS > 0
		uniform mat4 pointShadowMatrix[ NUM_POINT_LIGHT_SHADOWS ];
		varying vec4 vPointShadowCoord[ NUM_POINT_LIGHT_SHADOWS ];
		struct PointLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
			float shadowCameraNear;
			float shadowCameraFar;
		};
		uniform PointLightShadow pointLightShadows[ NUM_POINT_LIGHT_SHADOWS ];
	#endif
#endif`,Nd=`#if ( defined( USE_SHADOWMAP ) && ( NUM_DIR_LIGHT_SHADOWS > 0 || NUM_POINT_LIGHT_SHADOWS > 0 ) ) || ( NUM_SPOT_LIGHT_COORDS > 0 )
	vec3 shadowWorldNormal = inverseTransformDirection( transformedNormal, viewMatrix );
	vec4 shadowWorldPosition;
#endif
#if defined( USE_SHADOWMAP )
	#if NUM_DIR_LIGHT_SHADOWS > 0
		#pragma unroll_loop_start
		for ( int i = 0; i < NUM_DIR_LIGHT_SHADOWS; i ++ ) {
			shadowWorldPosition = worldPosition + vec4( shadowWorldNormal * directionalLightShadows[ i ].shadowNormalBias, 0 );
			vDirectionalShadowCoord[ i ] = directionalShadowMatrix[ i ] * shadowWorldPosition;
		}
		#pragma unroll_loop_end
	#endif
	#if NUM_POINT_LIGHT_SHADOWS > 0
		#pragma unroll_loop_start
		for ( int i = 0; i < NUM_POINT_LIGHT_SHADOWS; i ++ ) {
			shadowWorldPosition = worldPosition + vec4( shadowWorldNormal * pointLightShadows[ i ].shadowNormalBias, 0 );
			vPointShadowCoord[ i ] = pointShadowMatrix[ i ] * shadowWorldPosition;
		}
		#pragma unroll_loop_end
	#endif
#endif
#if NUM_SPOT_LIGHT_COORDS > 0
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_SPOT_LIGHT_COORDS; i ++ ) {
		shadowWorldPosition = worldPosition;
		#if ( defined( USE_SHADOWMAP ) && UNROLLED_LOOP_INDEX < NUM_SPOT_LIGHT_SHADOWS )
			shadowWorldPosition.xyz += shadowWorldNormal * spotLightShadows[ i ].shadowNormalBias;
		#endif
		vSpotLightCoord[ i ] = spotLightMatrix[ i ] * shadowWorldPosition;
	}
	#pragma unroll_loop_end
#endif`,Fd=`float getShadowMask() {
	float shadow = 1.0;
	#ifdef USE_SHADOWMAP
	#if NUM_DIR_LIGHT_SHADOWS > 0
	DirectionalLightShadow directionalLight;
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_DIR_LIGHT_SHADOWS; i ++ ) {
		directionalLight = directionalLightShadows[ i ];
		shadow *= receiveShadow ? getShadow( directionalShadowMap[ i ], directionalLight.shadowMapSize, directionalLight.shadowIntensity, directionalLight.shadowBias, directionalLight.shadowRadius, vDirectionalShadowCoord[ i ] ) : 1.0;
	}
	#pragma unroll_loop_end
	#endif
	#if NUM_SPOT_LIGHT_SHADOWS > 0
	SpotLightShadow spotLight;
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_SPOT_LIGHT_SHADOWS; i ++ ) {
		spotLight = spotLightShadows[ i ];
		shadow *= receiveShadow ? getShadow( spotShadowMap[ i ], spotLight.shadowMapSize, spotLight.shadowIntensity, spotLight.shadowBias, spotLight.shadowRadius, vSpotLightCoord[ i ] ) : 1.0;
	}
	#pragma unroll_loop_end
	#endif
	#if NUM_POINT_LIGHT_SHADOWS > 0
	PointLightShadow pointLight;
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_POINT_LIGHT_SHADOWS; i ++ ) {
		pointLight = pointLightShadows[ i ];
		shadow *= receiveShadow ? getPointShadow( pointShadowMap[ i ], pointLight.shadowMapSize, pointLight.shadowIntensity, pointLight.shadowBias, pointLight.shadowRadius, vPointShadowCoord[ i ], pointLight.shadowCameraNear, pointLight.shadowCameraFar ) : 1.0;
	}
	#pragma unroll_loop_end
	#endif
	#endif
	return shadow;
}`,Od=`#ifdef USE_SKINNING
	mat4 boneMatX = getBoneMatrix( skinIndex.x );
	mat4 boneMatY = getBoneMatrix( skinIndex.y );
	mat4 boneMatZ = getBoneMatrix( skinIndex.z );
	mat4 boneMatW = getBoneMatrix( skinIndex.w );
#endif`,Bd=`#ifdef USE_SKINNING
	uniform mat4 bindMatrix;
	uniform mat4 bindMatrixInverse;
	uniform highp sampler2D boneTexture;
	mat4 getBoneMatrix( const in float i ) {
		int size = textureSize( boneTexture, 0 ).x;
		int j = int( i ) * 4;
		int x = j % size;
		int y = j / size;
		vec4 v1 = texelFetch( boneTexture, ivec2( x, y ), 0 );
		vec4 v2 = texelFetch( boneTexture, ivec2( x + 1, y ), 0 );
		vec4 v3 = texelFetch( boneTexture, ivec2( x + 2, y ), 0 );
		vec4 v4 = texelFetch( boneTexture, ivec2( x + 3, y ), 0 );
		return mat4( v1, v2, v3, v4 );
	}
#endif`,zd=`#ifdef USE_SKINNING
	vec4 skinVertex = bindMatrix * vec4( transformed, 1.0 );
	vec4 skinned = vec4( 0.0 );
	skinned += boneMatX * skinVertex * skinWeight.x;
	skinned += boneMatY * skinVertex * skinWeight.y;
	skinned += boneMatZ * skinVertex * skinWeight.z;
	skinned += boneMatW * skinVertex * skinWeight.w;
	transformed = ( bindMatrixInverse * skinned ).xyz;
#endif`,kd=`#ifdef USE_SKINNING
	mat4 skinMatrix = mat4( 0.0 );
	skinMatrix += skinWeight.x * boneMatX;
	skinMatrix += skinWeight.y * boneMatY;
	skinMatrix += skinWeight.z * boneMatZ;
	skinMatrix += skinWeight.w * boneMatW;
	skinMatrix = bindMatrixInverse * skinMatrix * bindMatrix;
	objectNormal = vec4( skinMatrix * vec4( objectNormal, 0.0 ) ).xyz;
	#ifdef USE_TANGENT
		objectTangent = vec4( skinMatrix * vec4( objectTangent, 0.0 ) ).xyz;
	#endif
#endif`,Hd=`float specularStrength;
#ifdef USE_SPECULARMAP
	vec4 texelSpecular = texture2D( specularMap, vSpecularMapUv );
	specularStrength = texelSpecular.r;
#else
	specularStrength = 1.0;
#endif`,Vd=`#ifdef USE_SPECULARMAP
	uniform sampler2D specularMap;
#endif`,Gd=`#if defined( TONE_MAPPING )
	gl_FragColor.rgb = toneMapping( gl_FragColor.rgb );
#endif`,Wd=`#ifndef saturate
#define saturate( a ) clamp( a, 0.0, 1.0 )
#endif
uniform float toneMappingExposure;
vec3 LinearToneMapping( vec3 color ) {
	return saturate( toneMappingExposure * color );
}
vec3 ReinhardToneMapping( vec3 color ) {
	color *= toneMappingExposure;
	return saturate( color / ( vec3( 1.0 ) + color ) );
}
vec3 CineonToneMapping( vec3 color ) {
	color *= toneMappingExposure;
	color = max( vec3( 0.0 ), color - 0.004 );
	return pow( ( color * ( 6.2 * color + 0.5 ) ) / ( color * ( 6.2 * color + 1.7 ) + 0.06 ), vec3( 2.2 ) );
}
vec3 RRTAndODTFit( vec3 v ) {
	vec3 a = v * ( v + 0.0245786 ) - 0.000090537;
	vec3 b = v * ( 0.983729 * v + 0.4329510 ) + 0.238081;
	return a / b;
}
vec3 ACESFilmicToneMapping( vec3 color ) {
	const mat3 ACESInputMat = mat3(
		vec3( 0.59719, 0.07600, 0.02840 ),		vec3( 0.35458, 0.90834, 0.13383 ),
		vec3( 0.04823, 0.01566, 0.83777 )
	);
	const mat3 ACESOutputMat = mat3(
		vec3(  1.60475, -0.10208, -0.00327 ),		vec3( -0.53108,  1.10813, -0.07276 ),
		vec3( -0.07367, -0.00605,  1.07602 )
	);
	color *= toneMappingExposure / 0.6;
	color = ACESInputMat * color;
	color = RRTAndODTFit( color );
	color = ACESOutputMat * color;
	return saturate( color );
}
const mat3 LINEAR_REC2020_TO_LINEAR_SRGB = mat3(
	vec3( 1.6605, - 0.1246, - 0.0182 ),
	vec3( - 0.5876, 1.1329, - 0.1006 ),
	vec3( - 0.0728, - 0.0083, 1.1187 )
);
const mat3 LINEAR_SRGB_TO_LINEAR_REC2020 = mat3(
	vec3( 0.6274, 0.0691, 0.0164 ),
	vec3( 0.3293, 0.9195, 0.0880 ),
	vec3( 0.0433, 0.0113, 0.8956 )
);
vec3 agxDefaultContrastApprox( vec3 x ) {
	vec3 x2 = x * x;
	vec3 x4 = x2 * x2;
	return + 15.5 * x4 * x2
		- 40.14 * x4 * x
		+ 31.96 * x4
		- 6.868 * x2 * x
		+ 0.4298 * x2
		+ 0.1191 * x
		- 0.00232;
}
vec3 AgXToneMapping( vec3 color ) {
	const mat3 AgXInsetMatrix = mat3(
		vec3( 0.856627153315983, 0.137318972929847, 0.11189821299995 ),
		vec3( 0.0951212405381588, 0.761241990602591, 0.0767994186031903 ),
		vec3( 0.0482516061458583, 0.101439036467562, 0.811302368396859 )
	);
	const mat3 AgXOutsetMatrix = mat3(
		vec3( 1.1271005818144368, - 0.1413297634984383, - 0.14132976349843826 ),
		vec3( - 0.11060664309660323, 1.157823702216272, - 0.11060664309660294 ),
		vec3( - 0.016493938717834573, - 0.016493938717834257, 1.2519364065950405 )
	);
	const float AgxMinEv = - 12.47393;	const float AgxMaxEv = 4.026069;
	color *= toneMappingExposure;
	color = LINEAR_SRGB_TO_LINEAR_REC2020 * color;
	color = AgXInsetMatrix * color;
	color = max( color, 1e-10 );	color = log2( color );
	color = ( color - AgxMinEv ) / ( AgxMaxEv - AgxMinEv );
	color = clamp( color, 0.0, 1.0 );
	color = agxDefaultContrastApprox( color );
	color = AgXOutsetMatrix * color;
	color = pow( max( vec3( 0.0 ), color ), vec3( 2.2 ) );
	color = LINEAR_REC2020_TO_LINEAR_SRGB * color;
	color = clamp( color, 0.0, 1.0 );
	return color;
}
vec3 NeutralToneMapping( vec3 color ) {
	const float StartCompression = 0.8 - 0.04;
	const float Desaturation = 0.15;
	color *= toneMappingExposure;
	float x = min( color.r, min( color.g, color.b ) );
	float offset = x < 0.08 ? x - 6.25 * x * x : 0.04;
	color -= offset;
	float peak = max( color.r, max( color.g, color.b ) );
	if ( peak < StartCompression ) return color;
	float d = 1. - StartCompression;
	float newPeak = 1. - d * d / ( peak + d - StartCompression );
	color *= newPeak / peak;
	float g = 1. - 1. / ( Desaturation * ( peak - newPeak ) + 1. );
	return mix( color, vec3( newPeak ), g );
}
vec3 CustomToneMapping( vec3 color ) { return color; }`,Xd=`#ifdef USE_TRANSMISSION
	material.transmission = transmission;
	material.transmissionAlpha = 1.0;
	material.thickness = thickness;
	material.attenuationDistance = attenuationDistance;
	material.attenuationColor = attenuationColor;
	#ifdef USE_TRANSMISSIONMAP
		material.transmission *= texture2D( transmissionMap, vTransmissionMapUv ).r;
	#endif
	#ifdef USE_THICKNESSMAP
		material.thickness *= texture2D( thicknessMap, vThicknessMapUv ).g;
	#endif
	vec3 pos = vWorldPosition;
	vec3 v = normalize( cameraPosition - pos );
	vec3 n = inverseTransformDirection( normal, viewMatrix );
	vec4 transmitted = getIBLVolumeRefraction(
		n, v, material.roughness, material.diffuseColor, material.specularColor, material.specularF90,
		pos, modelMatrix, viewMatrix, projectionMatrix, material.dispersion, material.ior, material.thickness,
		material.attenuationColor, material.attenuationDistance );
	material.transmissionAlpha = mix( material.transmissionAlpha, transmitted.a, material.transmission );
	totalDiffuse = mix( totalDiffuse, transmitted.rgb, material.transmission );
#endif`,qd=`#ifdef USE_TRANSMISSION
	uniform float transmission;
	uniform float thickness;
	uniform float attenuationDistance;
	uniform vec3 attenuationColor;
	#ifdef USE_TRANSMISSIONMAP
		uniform sampler2D transmissionMap;
	#endif
	#ifdef USE_THICKNESSMAP
		uniform sampler2D thicknessMap;
	#endif
	uniform vec2 transmissionSamplerSize;
	uniform sampler2D transmissionSamplerMap;
	uniform mat4 modelMatrix;
	uniform mat4 projectionMatrix;
	varying vec3 vWorldPosition;
	float w0( float a ) {
		return ( 1.0 / 6.0 ) * ( a * ( a * ( - a + 3.0 ) - 3.0 ) + 1.0 );
	}
	float w1( float a ) {
		return ( 1.0 / 6.0 ) * ( a *  a * ( 3.0 * a - 6.0 ) + 4.0 );
	}
	float w2( float a ){
		return ( 1.0 / 6.0 ) * ( a * ( a * ( - 3.0 * a + 3.0 ) + 3.0 ) + 1.0 );
	}
	float w3( float a ) {
		return ( 1.0 / 6.0 ) * ( a * a * a );
	}
	float g0( float a ) {
		return w0( a ) + w1( a );
	}
	float g1( float a ) {
		return w2( a ) + w3( a );
	}
	float h0( float a ) {
		return - 1.0 + w1( a ) / ( w0( a ) + w1( a ) );
	}
	float h1( float a ) {
		return 1.0 + w3( a ) / ( w2( a ) + w3( a ) );
	}
	vec4 bicubic( sampler2D tex, vec2 uv, vec4 texelSize, float lod ) {
		uv = uv * texelSize.zw + 0.5;
		vec2 iuv = floor( uv );
		vec2 fuv = fract( uv );
		float g0x = g0( fuv.x );
		float g1x = g1( fuv.x );
		float h0x = h0( fuv.x );
		float h1x = h1( fuv.x );
		float h0y = h0( fuv.y );
		float h1y = h1( fuv.y );
		vec2 p0 = ( vec2( iuv.x + h0x, iuv.y + h0y ) - 0.5 ) * texelSize.xy;
		vec2 p1 = ( vec2( iuv.x + h1x, iuv.y + h0y ) - 0.5 ) * texelSize.xy;
		vec2 p2 = ( vec2( iuv.x + h0x, iuv.y + h1y ) - 0.5 ) * texelSize.xy;
		vec2 p3 = ( vec2( iuv.x + h1x, iuv.y + h1y ) - 0.5 ) * texelSize.xy;
		return g0( fuv.y ) * ( g0x * textureLod( tex, p0, lod ) + g1x * textureLod( tex, p1, lod ) ) +
			g1( fuv.y ) * ( g0x * textureLod( tex, p2, lod ) + g1x * textureLod( tex, p3, lod ) );
	}
	vec4 textureBicubic( sampler2D sampler, vec2 uv, float lod ) {
		vec2 fLodSize = vec2( textureSize( sampler, int( lod ) ) );
		vec2 cLodSize = vec2( textureSize( sampler, int( lod + 1.0 ) ) );
		vec2 fLodSizeInv = 1.0 / fLodSize;
		vec2 cLodSizeInv = 1.0 / cLodSize;
		vec4 fSample = bicubic( sampler, uv, vec4( fLodSizeInv, fLodSize ), floor( lod ) );
		vec4 cSample = bicubic( sampler, uv, vec4( cLodSizeInv, cLodSize ), ceil( lod ) );
		return mix( fSample, cSample, fract( lod ) );
	}
	vec3 getVolumeTransmissionRay( const in vec3 n, const in vec3 v, const in float thickness, const in float ior, const in mat4 modelMatrix ) {
		vec3 refractionVector = refract( - v, normalize( n ), 1.0 / ior );
		vec3 modelScale;
		modelScale.x = length( vec3( modelMatrix[ 0 ].xyz ) );
		modelScale.y = length( vec3( modelMatrix[ 1 ].xyz ) );
		modelScale.z = length( vec3( modelMatrix[ 2 ].xyz ) );
		return normalize( refractionVector ) * thickness * modelScale;
	}
	float applyIorToRoughness( const in float roughness, const in float ior ) {
		return roughness * clamp( ior * 2.0 - 2.0, 0.0, 1.0 );
	}
	vec4 getTransmissionSample( const in vec2 fragCoord, const in float roughness, const in float ior ) {
		float lod = log2( transmissionSamplerSize.x ) * applyIorToRoughness( roughness, ior );
		return textureBicubic( transmissionSamplerMap, fragCoord.xy, lod );
	}
	vec3 volumeAttenuation( const in float transmissionDistance, const in vec3 attenuationColor, const in float attenuationDistance ) {
		if ( isinf( attenuationDistance ) ) {
			return vec3( 1.0 );
		} else {
			vec3 attenuationCoefficient = -log( attenuationColor ) / attenuationDistance;
			vec3 transmittance = exp( - attenuationCoefficient * transmissionDistance );			return transmittance;
		}
	}
	vec4 getIBLVolumeRefraction( const in vec3 n, const in vec3 v, const in float roughness, const in vec3 diffuseColor,
		const in vec3 specularColor, const in float specularF90, const in vec3 position, const in mat4 modelMatrix,
		const in mat4 viewMatrix, const in mat4 projMatrix, const in float dispersion, const in float ior, const in float thickness,
		const in vec3 attenuationColor, const in float attenuationDistance ) {
		vec4 transmittedLight;
		vec3 transmittance;
		#ifdef USE_DISPERSION
			float halfSpread = ( ior - 1.0 ) * 0.025 * dispersion;
			vec3 iors = vec3( ior - halfSpread, ior, ior + halfSpread );
			for ( int i = 0; i < 3; i ++ ) {
				vec3 transmissionRay = getVolumeTransmissionRay( n, v, thickness, iors[ i ], modelMatrix );
				vec3 refractedRayExit = position + transmissionRay;
				vec4 ndcPos = projMatrix * viewMatrix * vec4( refractedRayExit, 1.0 );
				vec2 refractionCoords = ndcPos.xy / ndcPos.w;
				refractionCoords += 1.0;
				refractionCoords /= 2.0;
				vec4 transmissionSample = getTransmissionSample( refractionCoords, roughness, iors[ i ] );
				transmittedLight[ i ] = transmissionSample[ i ];
				transmittedLight.a += transmissionSample.a;
				transmittance[ i ] = diffuseColor[ i ] * volumeAttenuation( length( transmissionRay ), attenuationColor, attenuationDistance )[ i ];
			}
			transmittedLight.a /= 3.0;
		#else
			vec3 transmissionRay = getVolumeTransmissionRay( n, v, thickness, ior, modelMatrix );
			vec3 refractedRayExit = position + transmissionRay;
			vec4 ndcPos = projMatrix * viewMatrix * vec4( refractedRayExit, 1.0 );
			vec2 refractionCoords = ndcPos.xy / ndcPos.w;
			refractionCoords += 1.0;
			refractionCoords /= 2.0;
			transmittedLight = getTransmissionSample( refractionCoords, roughness, ior );
			transmittance = diffuseColor * volumeAttenuation( length( transmissionRay ), attenuationColor, attenuationDistance );
		#endif
		vec3 attenuatedColor = transmittance * transmittedLight.rgb;
		vec3 F = EnvironmentBRDF( n, v, specularColor, specularF90, roughness );
		float transmittanceFactor = ( transmittance.r + transmittance.g + transmittance.b ) / 3.0;
		return vec4( ( 1.0 - F ) * attenuatedColor, 1.0 - ( 1.0 - transmittedLight.a ) * transmittanceFactor );
	}
#endif`,Yd=`#if defined( USE_UV ) || defined( USE_ANISOTROPY )
	varying vec2 vUv;
#endif
#ifdef USE_MAP
	varying vec2 vMapUv;
#endif
#ifdef USE_ALPHAMAP
	varying vec2 vAlphaMapUv;
#endif
#ifdef USE_LIGHTMAP
	varying vec2 vLightMapUv;
#endif
#ifdef USE_AOMAP
	varying vec2 vAoMapUv;
#endif
#ifdef USE_BUMPMAP
	varying vec2 vBumpMapUv;
#endif
#ifdef USE_NORMALMAP
	varying vec2 vNormalMapUv;
#endif
#ifdef USE_EMISSIVEMAP
	varying vec2 vEmissiveMapUv;
#endif
#ifdef USE_METALNESSMAP
	varying vec2 vMetalnessMapUv;
#endif
#ifdef USE_ROUGHNESSMAP
	varying vec2 vRoughnessMapUv;
#endif
#ifdef USE_ANISOTROPYMAP
	varying vec2 vAnisotropyMapUv;
#endif
#ifdef USE_CLEARCOATMAP
	varying vec2 vClearcoatMapUv;
#endif
#ifdef USE_CLEARCOAT_NORMALMAP
	varying vec2 vClearcoatNormalMapUv;
#endif
#ifdef USE_CLEARCOAT_ROUGHNESSMAP
	varying vec2 vClearcoatRoughnessMapUv;
#endif
#ifdef USE_IRIDESCENCEMAP
	varying vec2 vIridescenceMapUv;
#endif
#ifdef USE_IRIDESCENCE_THICKNESSMAP
	varying vec2 vIridescenceThicknessMapUv;
#endif
#ifdef USE_SHEEN_COLORMAP
	varying vec2 vSheenColorMapUv;
#endif
#ifdef USE_SHEEN_ROUGHNESSMAP
	varying vec2 vSheenRoughnessMapUv;
#endif
#ifdef USE_SPECULARMAP
	varying vec2 vSpecularMapUv;
#endif
#ifdef USE_SPECULAR_COLORMAP
	varying vec2 vSpecularColorMapUv;
#endif
#ifdef USE_SPECULAR_INTENSITYMAP
	varying vec2 vSpecularIntensityMapUv;
#endif
#ifdef USE_TRANSMISSIONMAP
	uniform mat3 transmissionMapTransform;
	varying vec2 vTransmissionMapUv;
#endif
#ifdef USE_THICKNESSMAP
	uniform mat3 thicknessMapTransform;
	varying vec2 vThicknessMapUv;
#endif`,jd=`#if defined( USE_UV ) || defined( USE_ANISOTROPY )
	varying vec2 vUv;
#endif
#ifdef USE_MAP
	uniform mat3 mapTransform;
	varying vec2 vMapUv;
#endif
#ifdef USE_ALPHAMAP
	uniform mat3 alphaMapTransform;
	varying vec2 vAlphaMapUv;
#endif
#ifdef USE_LIGHTMAP
	uniform mat3 lightMapTransform;
	varying vec2 vLightMapUv;
#endif
#ifdef USE_AOMAP
	uniform mat3 aoMapTransform;
	varying vec2 vAoMapUv;
#endif
#ifdef USE_BUMPMAP
	uniform mat3 bumpMapTransform;
	varying vec2 vBumpMapUv;
#endif
#ifdef USE_NORMALMAP
	uniform mat3 normalMapTransform;
	varying vec2 vNormalMapUv;
#endif
#ifdef USE_DISPLACEMENTMAP
	uniform mat3 displacementMapTransform;
	varying vec2 vDisplacementMapUv;
#endif
#ifdef USE_EMISSIVEMAP
	uniform mat3 emissiveMapTransform;
	varying vec2 vEmissiveMapUv;
#endif
#ifdef USE_METALNESSMAP
	uniform mat3 metalnessMapTransform;
	varying vec2 vMetalnessMapUv;
#endif
#ifdef USE_ROUGHNESSMAP
	uniform mat3 roughnessMapTransform;
	varying vec2 vRoughnessMapUv;
#endif
#ifdef USE_ANISOTROPYMAP
	uniform mat3 anisotropyMapTransform;
	varying vec2 vAnisotropyMapUv;
#endif
#ifdef USE_CLEARCOATMAP
	uniform mat3 clearcoatMapTransform;
	varying vec2 vClearcoatMapUv;
#endif
#ifdef USE_CLEARCOAT_NORMALMAP
	uniform mat3 clearcoatNormalMapTransform;
	varying vec2 vClearcoatNormalMapUv;
#endif
#ifdef USE_CLEARCOAT_ROUGHNESSMAP
	uniform mat3 clearcoatRoughnessMapTransform;
	varying vec2 vClearcoatRoughnessMapUv;
#endif
#ifdef USE_SHEEN_COLORMAP
	uniform mat3 sheenColorMapTransform;
	varying vec2 vSheenColorMapUv;
#endif
#ifdef USE_SHEEN_ROUGHNESSMAP
	uniform mat3 sheenRoughnessMapTransform;
	varying vec2 vSheenRoughnessMapUv;
#endif
#ifdef USE_IRIDESCENCEMAP
	uniform mat3 iridescenceMapTransform;
	varying vec2 vIridescenceMapUv;
#endif
#ifdef USE_IRIDESCENCE_THICKNESSMAP
	uniform mat3 iridescenceThicknessMapTransform;
	varying vec2 vIridescenceThicknessMapUv;
#endif
#ifdef USE_SPECULARMAP
	uniform mat3 specularMapTransform;
	varying vec2 vSpecularMapUv;
#endif
#ifdef USE_SPECULAR_COLORMAP
	uniform mat3 specularColorMapTransform;
	varying vec2 vSpecularColorMapUv;
#endif
#ifdef USE_SPECULAR_INTENSITYMAP
	uniform mat3 specularIntensityMapTransform;
	varying vec2 vSpecularIntensityMapUv;
#endif
#ifdef USE_TRANSMISSIONMAP
	uniform mat3 transmissionMapTransform;
	varying vec2 vTransmissionMapUv;
#endif
#ifdef USE_THICKNESSMAP
	uniform mat3 thicknessMapTransform;
	varying vec2 vThicknessMapUv;
#endif`,Kd=`#if defined( USE_UV ) || defined( USE_ANISOTROPY )
	vUv = vec3( uv, 1 ).xy;
#endif
#ifdef USE_MAP
	vMapUv = ( mapTransform * vec3( MAP_UV, 1 ) ).xy;
#endif
#ifdef USE_ALPHAMAP
	vAlphaMapUv = ( alphaMapTransform * vec3( ALPHAMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_LIGHTMAP
	vLightMapUv = ( lightMapTransform * vec3( LIGHTMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_AOMAP
	vAoMapUv = ( aoMapTransform * vec3( AOMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_BUMPMAP
	vBumpMapUv = ( bumpMapTransform * vec3( BUMPMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_NORMALMAP
	vNormalMapUv = ( normalMapTransform * vec3( NORMALMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_DISPLACEMENTMAP
	vDisplacementMapUv = ( displacementMapTransform * vec3( DISPLACEMENTMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_EMISSIVEMAP
	vEmissiveMapUv = ( emissiveMapTransform * vec3( EMISSIVEMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_METALNESSMAP
	vMetalnessMapUv = ( metalnessMapTransform * vec3( METALNESSMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_ROUGHNESSMAP
	vRoughnessMapUv = ( roughnessMapTransform * vec3( ROUGHNESSMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_ANISOTROPYMAP
	vAnisotropyMapUv = ( anisotropyMapTransform * vec3( ANISOTROPYMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_CLEARCOATMAP
	vClearcoatMapUv = ( clearcoatMapTransform * vec3( CLEARCOATMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_CLEARCOAT_NORMALMAP
	vClearcoatNormalMapUv = ( clearcoatNormalMapTransform * vec3( CLEARCOAT_NORMALMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_CLEARCOAT_ROUGHNESSMAP
	vClearcoatRoughnessMapUv = ( clearcoatRoughnessMapTransform * vec3( CLEARCOAT_ROUGHNESSMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_IRIDESCENCEMAP
	vIridescenceMapUv = ( iridescenceMapTransform * vec3( IRIDESCENCEMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_IRIDESCENCE_THICKNESSMAP
	vIridescenceThicknessMapUv = ( iridescenceThicknessMapTransform * vec3( IRIDESCENCE_THICKNESSMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_SHEEN_COLORMAP
	vSheenColorMapUv = ( sheenColorMapTransform * vec3( SHEEN_COLORMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_SHEEN_ROUGHNESSMAP
	vSheenRoughnessMapUv = ( sheenRoughnessMapTransform * vec3( SHEEN_ROUGHNESSMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_SPECULARMAP
	vSpecularMapUv = ( specularMapTransform * vec3( SPECULARMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_SPECULAR_COLORMAP
	vSpecularColorMapUv = ( specularColorMapTransform * vec3( SPECULAR_COLORMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_SPECULAR_INTENSITYMAP
	vSpecularIntensityMapUv = ( specularIntensityMapTransform * vec3( SPECULAR_INTENSITYMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_TRANSMISSIONMAP
	vTransmissionMapUv = ( transmissionMapTransform * vec3( TRANSMISSIONMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_THICKNESSMAP
	vThicknessMapUv = ( thicknessMapTransform * vec3( THICKNESSMAP_UV, 1 ) ).xy;
#endif`,Zd=`#if defined( USE_ENVMAP ) || defined( DISTANCE ) || defined ( USE_SHADOWMAP ) || defined ( USE_TRANSMISSION ) || NUM_SPOT_LIGHT_COORDS > 0
	vec4 worldPosition = vec4( transformed, 1.0 );
	#ifdef USE_BATCHING
		worldPosition = batchingMatrix * worldPosition;
	#endif
	#ifdef USE_INSTANCING
		worldPosition = instanceMatrix * worldPosition;
	#endif
	worldPosition = modelMatrix * worldPosition;
#endif`;const $d=`varying vec2 vUv;
uniform mat3 uvTransform;
void main() {
	vUv = ( uvTransform * vec3( uv, 1 ) ).xy;
	gl_Position = vec4( position.xy, 1.0, 1.0 );
}`,Jd=`uniform sampler2D t2D;
uniform float backgroundIntensity;
varying vec2 vUv;
void main() {
	vec4 texColor = texture2D( t2D, vUv );
	#ifdef DECODE_VIDEO_TEXTURE
		texColor = vec4( mix( pow( texColor.rgb * 0.9478672986 + vec3( 0.0521327014 ), vec3( 2.4 ) ), texColor.rgb * 0.0773993808, vec3( lessThanEqual( texColor.rgb, vec3( 0.04045 ) ) ) ), texColor.w );
	#endif
	texColor.rgb *= backgroundIntensity;
	gl_FragColor = texColor;
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
}`,Qd=`varying vec3 vWorldDirection;
#include <common>
void main() {
	vWorldDirection = transformDirection( position, modelMatrix );
	#include <begin_vertex>
	#include <project_vertex>
	gl_Position.z = gl_Position.w;
}`,tf=`#ifdef ENVMAP_TYPE_CUBE
	uniform samplerCube envMap;
#elif defined( ENVMAP_TYPE_CUBE_UV )
	uniform sampler2D envMap;
#endif
uniform float flipEnvMap;
uniform float backgroundBlurriness;
uniform float backgroundIntensity;
uniform mat3 backgroundRotation;
varying vec3 vWorldDirection;
#include <cube_uv_reflection_fragment>
void main() {
	#ifdef ENVMAP_TYPE_CUBE
		vec4 texColor = textureCube( envMap, backgroundRotation * vec3( flipEnvMap * vWorldDirection.x, vWorldDirection.yz ) );
	#elif defined( ENVMAP_TYPE_CUBE_UV )
		vec4 texColor = textureCubeUV( envMap, backgroundRotation * vWorldDirection, backgroundBlurriness );
	#else
		vec4 texColor = vec4( 0.0, 0.0, 0.0, 1.0 );
	#endif
	texColor.rgb *= backgroundIntensity;
	gl_FragColor = texColor;
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
}`,ef=`varying vec3 vWorldDirection;
#include <common>
void main() {
	vWorldDirection = transformDirection( position, modelMatrix );
	#include <begin_vertex>
	#include <project_vertex>
	gl_Position.z = gl_Position.w;
}`,nf=`uniform samplerCube tCube;
uniform float tFlip;
uniform float opacity;
varying vec3 vWorldDirection;
void main() {
	vec4 texColor = textureCube( tCube, vec3( tFlip * vWorldDirection.x, vWorldDirection.yz ) );
	gl_FragColor = texColor;
	gl_FragColor.a *= opacity;
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
}`,sf=`#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
varying vec2 vHighPrecisionZW;
void main() {
	#include <uv_vertex>
	#include <batching_vertex>
	#include <skinbase_vertex>
	#include <morphinstance_vertex>
	#ifdef USE_DISPLACEMENTMAP
		#include <beginnormal_vertex>
		#include <morphnormal_vertex>
		#include <skinnormal_vertex>
	#endif
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	vHighPrecisionZW = gl_Position.zw;
}`,rf=`#if DEPTH_PACKING == 3200
	uniform float opacity;
#endif
#include <common>
#include <packing>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
varying vec2 vHighPrecisionZW;
void main() {
	vec4 diffuseColor = vec4( 1.0 );
	#include <clipping_planes_fragment>
	#if DEPTH_PACKING == 3200
		diffuseColor.a = opacity;
	#endif
	#include <map_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <logdepthbuf_fragment>
	#ifdef USE_REVERSED_DEPTH_BUFFER
		float fragCoordZ = vHighPrecisionZW[ 0 ] / vHighPrecisionZW[ 1 ];
	#else
		float fragCoordZ = 0.5 * vHighPrecisionZW[ 0 ] / vHighPrecisionZW[ 1 ] + 0.5;
	#endif
	#if DEPTH_PACKING == 3200
		gl_FragColor = vec4( vec3( 1.0 - fragCoordZ ), opacity );
	#elif DEPTH_PACKING == 3201
		gl_FragColor = packDepthToRGBA( fragCoordZ );
	#elif DEPTH_PACKING == 3202
		gl_FragColor = vec4( packDepthToRGB( fragCoordZ ), 1.0 );
	#elif DEPTH_PACKING == 3203
		gl_FragColor = vec4( packDepthToRG( fragCoordZ ), 0.0, 1.0 );
	#endif
}`,af=`#define DISTANCE
varying vec3 vWorldPosition;
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <batching_vertex>
	#include <skinbase_vertex>
	#include <morphinstance_vertex>
	#ifdef USE_DISPLACEMENTMAP
		#include <beginnormal_vertex>
		#include <morphnormal_vertex>
		#include <skinnormal_vertex>
	#endif
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <worldpos_vertex>
	#include <clipping_planes_vertex>
	vWorldPosition = worldPosition.xyz;
}`,of=`#define DISTANCE
uniform vec3 referencePosition;
uniform float nearDistance;
uniform float farDistance;
varying vec3 vWorldPosition;
#include <common>
#include <packing>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <clipping_planes_pars_fragment>
void main () {
	vec4 diffuseColor = vec4( 1.0 );
	#include <clipping_planes_fragment>
	#include <map_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	float dist = length( vWorldPosition - referencePosition );
	dist = ( dist - nearDistance ) / ( farDistance - nearDistance );
	dist = saturate( dist );
	gl_FragColor = packDepthToRGBA( dist );
}`,lf=`varying vec3 vWorldDirection;
#include <common>
void main() {
	vWorldDirection = transformDirection( position, modelMatrix );
	#include <begin_vertex>
	#include <project_vertex>
}`,cf=`uniform sampler2D tEquirect;
varying vec3 vWorldDirection;
#include <common>
void main() {
	vec3 direction = normalize( vWorldDirection );
	vec2 sampleUV = equirectUv( direction );
	gl_FragColor = texture2D( tEquirect, sampleUV );
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
}`,hf=`uniform float scale;
attribute float lineDistance;
varying float vLineDistance;
#include <common>
#include <uv_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <morphtarget_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	vLineDistance = scale * lineDistance;
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	#include <fog_vertex>
}`,uf=`uniform vec3 diffuse;
uniform float opacity;
uniform float dashSize;
uniform float totalSize;
varying float vLineDistance;
#include <common>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <fog_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	if ( mod( vLineDistance, totalSize ) > dashSize ) {
		discard;
	}
	vec3 outgoingLight = vec3( 0.0 );
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	outgoingLight = diffuseColor.rgb;
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
}`,df=`#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <envmap_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#if defined ( USE_ENVMAP ) || defined ( USE_SKINNING )
		#include <beginnormal_vertex>
		#include <morphnormal_vertex>
		#include <skinbase_vertex>
		#include <skinnormal_vertex>
		#include <defaultnormal_vertex>
	#endif
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	#include <worldpos_vertex>
	#include <envmap_vertex>
	#include <fog_vertex>
}`,ff=`uniform vec3 diffuse;
uniform float opacity;
#ifndef FLAT_SHADED
	varying vec3 vNormal;
#endif
#include <common>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <aomap_pars_fragment>
#include <lightmap_pars_fragment>
#include <envmap_common_pars_fragment>
#include <envmap_pars_fragment>
#include <fog_pars_fragment>
#include <specularmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <specularmap_fragment>
	ReflectedLight reflectedLight = ReflectedLight( vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ) );
	#ifdef USE_LIGHTMAP
		vec4 lightMapTexel = texture2D( lightMap, vLightMapUv );
		reflectedLight.indirectDiffuse += lightMapTexel.rgb * lightMapIntensity * RECIPROCAL_PI;
	#else
		reflectedLight.indirectDiffuse += vec3( 1.0 );
	#endif
	#include <aomap_fragment>
	reflectedLight.indirectDiffuse *= diffuseColor.rgb;
	vec3 outgoingLight = reflectedLight.indirectDiffuse;
	#include <envmap_fragment>
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,pf=`#define LAMBERT
varying vec3 vViewPosition;
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <envmap_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <shadowmap_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	vViewPosition = - mvPosition.xyz;
	#include <worldpos_vertex>
	#include <envmap_vertex>
	#include <shadowmap_vertex>
	#include <fog_vertex>
}`,mf=`#define LAMBERT
uniform vec3 diffuse;
uniform vec3 emissive;
uniform float opacity;
#include <common>
#include <packing>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <aomap_pars_fragment>
#include <lightmap_pars_fragment>
#include <emissivemap_pars_fragment>
#include <envmap_common_pars_fragment>
#include <envmap_pars_fragment>
#include <fog_pars_fragment>
#include <bsdfs>
#include <lights_pars_begin>
#include <normal_pars_fragment>
#include <lights_lambert_pars_fragment>
#include <shadowmap_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <specularmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	ReflectedLight reflectedLight = ReflectedLight( vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ) );
	vec3 totalEmissiveRadiance = emissive;
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <specularmap_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	#include <emissivemap_fragment>
	#include <lights_lambert_fragment>
	#include <lights_fragment_begin>
	#include <lights_fragment_maps>
	#include <lights_fragment_end>
	#include <aomap_fragment>
	vec3 outgoingLight = reflectedLight.directDiffuse + reflectedLight.indirectDiffuse + totalEmissiveRadiance;
	#include <envmap_fragment>
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,gf=`#define MATCAP
varying vec3 vViewPosition;
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <color_pars_vertex>
#include <displacementmap_pars_vertex>
#include <fog_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	#include <fog_vertex>
	vViewPosition = - mvPosition.xyz;
}`,_f=`#define MATCAP
uniform vec3 diffuse;
uniform float opacity;
uniform sampler2D matcap;
varying vec3 vViewPosition;
#include <common>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <fog_pars_fragment>
#include <normal_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	vec3 viewDir = normalize( vViewPosition );
	vec3 x = normalize( vec3( viewDir.z, 0.0, - viewDir.x ) );
	vec3 y = cross( viewDir, x );
	vec2 uv = vec2( dot( x, normal ), dot( y, normal ) ) * 0.495 + 0.5;
	#ifdef USE_MATCAP
		vec4 matcapColor = texture2D( matcap, uv );
	#else
		vec4 matcapColor = vec4( vec3( mix( 0.2, 0.8, uv.y ) ), 1.0 );
	#endif
	vec3 outgoingLight = diffuseColor.rgb * matcapColor.rgb;
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,xf=`#define NORMAL
#if defined( FLAT_SHADED ) || defined( USE_BUMPMAP ) || defined( USE_NORMALMAP_TANGENTSPACE )
	varying vec3 vViewPosition;
#endif
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphinstance_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
#if defined( FLAT_SHADED ) || defined( USE_BUMPMAP ) || defined( USE_NORMALMAP_TANGENTSPACE )
	vViewPosition = - mvPosition.xyz;
#endif
}`,vf=`#define NORMAL
uniform float opacity;
#if defined( FLAT_SHADED ) || defined( USE_BUMPMAP ) || defined( USE_NORMALMAP_TANGENTSPACE )
	varying vec3 vViewPosition;
#endif
#include <packing>
#include <uv_pars_fragment>
#include <normal_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( 0.0, 0.0, 0.0, opacity );
	#include <clipping_planes_fragment>
	#include <logdepthbuf_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	gl_FragColor = vec4( packNormalToRGB( normal ), diffuseColor.a );
	#ifdef OPAQUE
		gl_FragColor.a = 1.0;
	#endif
}`,yf=`#define PHONG
varying vec3 vViewPosition;
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <envmap_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <shadowmap_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphinstance_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	vViewPosition = - mvPosition.xyz;
	#include <worldpos_vertex>
	#include <envmap_vertex>
	#include <shadowmap_vertex>
	#include <fog_vertex>
}`,Mf=`#define PHONG
uniform vec3 diffuse;
uniform vec3 emissive;
uniform vec3 specular;
uniform float shininess;
uniform float opacity;
#include <common>
#include <packing>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <aomap_pars_fragment>
#include <lightmap_pars_fragment>
#include <emissivemap_pars_fragment>
#include <envmap_common_pars_fragment>
#include <envmap_pars_fragment>
#include <fog_pars_fragment>
#include <bsdfs>
#include <lights_pars_begin>
#include <normal_pars_fragment>
#include <lights_phong_pars_fragment>
#include <shadowmap_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <specularmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	ReflectedLight reflectedLight = ReflectedLight( vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ) );
	vec3 totalEmissiveRadiance = emissive;
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <specularmap_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	#include <emissivemap_fragment>
	#include <lights_phong_fragment>
	#include <lights_fragment_begin>
	#include <lights_fragment_maps>
	#include <lights_fragment_end>
	#include <aomap_fragment>
	vec3 outgoingLight = reflectedLight.directDiffuse + reflectedLight.indirectDiffuse + reflectedLight.directSpecular + reflectedLight.indirectSpecular + totalEmissiveRadiance;
	#include <envmap_fragment>
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,Sf=`#define STANDARD
varying vec3 vViewPosition;
#ifdef USE_TRANSMISSION
	varying vec3 vWorldPosition;
#endif
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <shadowmap_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	vViewPosition = - mvPosition.xyz;
	#include <worldpos_vertex>
	#include <shadowmap_vertex>
	#include <fog_vertex>
#ifdef USE_TRANSMISSION
	vWorldPosition = worldPosition.xyz;
#endif
}`,Ef=`#define STANDARD
#ifdef PHYSICAL
	#define IOR
	#define USE_SPECULAR
#endif
uniform vec3 diffuse;
uniform vec3 emissive;
uniform float roughness;
uniform float metalness;
uniform float opacity;
#ifdef IOR
	uniform float ior;
#endif
#ifdef USE_SPECULAR
	uniform float specularIntensity;
	uniform vec3 specularColor;
	#ifdef USE_SPECULAR_COLORMAP
		uniform sampler2D specularColorMap;
	#endif
	#ifdef USE_SPECULAR_INTENSITYMAP
		uniform sampler2D specularIntensityMap;
	#endif
#endif
#ifdef USE_CLEARCOAT
	uniform float clearcoat;
	uniform float clearcoatRoughness;
#endif
#ifdef USE_DISPERSION
	uniform float dispersion;
#endif
#ifdef USE_IRIDESCENCE
	uniform float iridescence;
	uniform float iridescenceIOR;
	uniform float iridescenceThicknessMinimum;
	uniform float iridescenceThicknessMaximum;
#endif
#ifdef USE_SHEEN
	uniform vec3 sheenColor;
	uniform float sheenRoughness;
	#ifdef USE_SHEEN_COLORMAP
		uniform sampler2D sheenColorMap;
	#endif
	#ifdef USE_SHEEN_ROUGHNESSMAP
		uniform sampler2D sheenRoughnessMap;
	#endif
#endif
#ifdef USE_ANISOTROPY
	uniform vec2 anisotropyVector;
	#ifdef USE_ANISOTROPYMAP
		uniform sampler2D anisotropyMap;
	#endif
#endif
varying vec3 vViewPosition;
#include <common>
#include <packing>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <aomap_pars_fragment>
#include <lightmap_pars_fragment>
#include <emissivemap_pars_fragment>
#include <iridescence_fragment>
#include <cube_uv_reflection_fragment>
#include <envmap_common_pars_fragment>
#include <envmap_physical_pars_fragment>
#include <fog_pars_fragment>
#include <lights_pars_begin>
#include <normal_pars_fragment>
#include <lights_physical_pars_fragment>
#include <transmission_pars_fragment>
#include <shadowmap_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <clearcoat_pars_fragment>
#include <iridescence_pars_fragment>
#include <roughnessmap_pars_fragment>
#include <metalnessmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	ReflectedLight reflectedLight = ReflectedLight( vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ) );
	vec3 totalEmissiveRadiance = emissive;
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <roughnessmap_fragment>
	#include <metalnessmap_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	#include <clearcoat_normal_fragment_begin>
	#include <clearcoat_normal_fragment_maps>
	#include <emissivemap_fragment>
	#include <lights_physical_fragment>
	#include <lights_fragment_begin>
	#include <lights_fragment_maps>
	#include <lights_fragment_end>
	#include <aomap_fragment>
	vec3 totalDiffuse = reflectedLight.directDiffuse + reflectedLight.indirectDiffuse;
	vec3 totalSpecular = reflectedLight.directSpecular + reflectedLight.indirectSpecular;
	#include <transmission_fragment>
	vec3 outgoingLight = totalDiffuse + totalSpecular + totalEmissiveRadiance;
	#ifdef USE_SHEEN
		float sheenEnergyComp = 1.0 - 0.157 * max3( material.sheenColor );
		outgoingLight = outgoingLight * sheenEnergyComp + sheenSpecularDirect + sheenSpecularIndirect;
	#endif
	#ifdef USE_CLEARCOAT
		float dotNVcc = saturate( dot( geometryClearcoatNormal, geometryViewDir ) );
		vec3 Fcc = F_Schlick( material.clearcoatF0, material.clearcoatF90, dotNVcc );
		outgoingLight = outgoingLight * ( 1.0 - material.clearcoat * Fcc ) + ( clearcoatSpecularDirect + clearcoatSpecularIndirect ) * material.clearcoat;
	#endif
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,bf=`#define TOON
varying vec3 vViewPosition;
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <shadowmap_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	vViewPosition = - mvPosition.xyz;
	#include <worldpos_vertex>
	#include <shadowmap_vertex>
	#include <fog_vertex>
}`,Tf=`#define TOON
uniform vec3 diffuse;
uniform vec3 emissive;
uniform float opacity;
#include <common>
#include <packing>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <aomap_pars_fragment>
#include <lightmap_pars_fragment>
#include <emissivemap_pars_fragment>
#include <gradientmap_pars_fragment>
#include <fog_pars_fragment>
#include <bsdfs>
#include <lights_pars_begin>
#include <normal_pars_fragment>
#include <lights_toon_pars_fragment>
#include <shadowmap_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	ReflectedLight reflectedLight = ReflectedLight( vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ) );
	vec3 totalEmissiveRadiance = emissive;
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	#include <emissivemap_fragment>
	#include <lights_toon_fragment>
	#include <lights_fragment_begin>
	#include <lights_fragment_maps>
	#include <lights_fragment_end>
	#include <aomap_fragment>
	vec3 outgoingLight = reflectedLight.directDiffuse + reflectedLight.indirectDiffuse + totalEmissiveRadiance;
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,wf=`uniform float size;
uniform float scale;
#include <common>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <morphtarget_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
#ifdef USE_POINTS_UV
	varying vec2 vUv;
	uniform mat3 uvTransform;
#endif
void main() {
	#ifdef USE_POINTS_UV
		vUv = ( uvTransform * vec3( uv, 1 ) ).xy;
	#endif
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <project_vertex>
	gl_PointSize = size;
	#ifdef USE_SIZEATTENUATION
		bool isPerspective = isPerspectiveMatrix( projectionMatrix );
		if ( isPerspective ) gl_PointSize *= ( scale / - mvPosition.z );
	#endif
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	#include <worldpos_vertex>
	#include <fog_vertex>
}`,Af=`uniform vec3 diffuse;
uniform float opacity;
#include <common>
#include <color_pars_fragment>
#include <map_particle_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <fog_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	vec3 outgoingLight = vec3( 0.0 );
	#include <logdepthbuf_fragment>
	#include <map_particle_fragment>
	#include <color_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	outgoingLight = diffuseColor.rgb;
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
}`,Cf=`#include <common>
#include <batching_pars_vertex>
#include <fog_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <shadowmap_pars_vertex>
void main() {
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphinstance_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <worldpos_vertex>
	#include <shadowmap_vertex>
	#include <fog_vertex>
}`,Rf=`uniform vec3 color;
uniform float opacity;
#include <common>
#include <packing>
#include <fog_pars_fragment>
#include <bsdfs>
#include <lights_pars_begin>
#include <logdepthbuf_pars_fragment>
#include <shadowmap_pars_fragment>
#include <shadowmask_pars_fragment>
void main() {
	#include <logdepthbuf_fragment>
	gl_FragColor = vec4( color, opacity * ( 1.0 - getShadowMask() ) );
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
}`,Pf=`uniform float rotation;
uniform vec2 center;
#include <common>
#include <uv_pars_vertex>
#include <fog_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	vec4 mvPosition = modelViewMatrix[ 3 ];
	vec2 scale = vec2( length( modelMatrix[ 0 ].xyz ), length( modelMatrix[ 1 ].xyz ) );
	#ifndef USE_SIZEATTENUATION
		bool isPerspective = isPerspectiveMatrix( projectionMatrix );
		if ( isPerspective ) scale *= - mvPosition.z;
	#endif
	vec2 alignedPosition = ( position.xy - ( center - vec2( 0.5 ) ) ) * scale;
	vec2 rotatedPosition;
	rotatedPosition.x = cos( rotation ) * alignedPosition.x - sin( rotation ) * alignedPosition.y;
	rotatedPosition.y = sin( rotation ) * alignedPosition.x + cos( rotation ) * alignedPosition.y;
	mvPosition.xy += rotatedPosition;
	gl_Position = projectionMatrix * mvPosition;
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	#include <fog_vertex>
}`,Lf=`uniform vec3 diffuse;
uniform float opacity;
#include <common>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <fog_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	vec3 outgoingLight = vec3( 0.0 );
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	outgoingLight = diffuseColor.rgb;
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
}`,Bt={alphahash_fragment:Jh,alphahash_pars_fragment:Qh,alphamap_fragment:tu,alphamap_pars_fragment:eu,alphatest_fragment:nu,alphatest_pars_fragment:iu,aomap_fragment:su,aomap_pars_fragment:ru,batching_pars_vertex:au,batching_vertex:ou,begin_vertex:lu,beginnormal_vertex:cu,bsdfs:hu,iridescence_fragment:uu,bumpmap_pars_fragment:du,clipping_planes_fragment:fu,clipping_planes_pars_fragment:pu,clipping_planes_pars_vertex:mu,clipping_planes_vertex:gu,color_fragment:_u,color_pars_fragment:xu,color_pars_vertex:vu,color_vertex:yu,common:Mu,cube_uv_reflection_fragment:Su,defaultnormal_vertex:Eu,displacementmap_pars_vertex:bu,displacementmap_vertex:Tu,emissivemap_fragment:wu,emissivemap_pars_fragment:Au,colorspace_fragment:Cu,colorspace_pars_fragment:Ru,envmap_fragment:Pu,envmap_common_pars_fragment:Lu,envmap_pars_fragment:Du,envmap_pars_vertex:Iu,envmap_physical_pars_fragment:Wu,envmap_vertex:Uu,fog_vertex:Nu,fog_pars_vertex:Fu,fog_fragment:Ou,fog_pars_fragment:Bu,gradientmap_pars_fragment:zu,lightmap_pars_fragment:ku,lights_lambert_fragment:Hu,lights_lambert_pars_fragment:Vu,lights_pars_begin:Gu,lights_toon_fragment:Xu,lights_toon_pars_fragment:qu,lights_phong_fragment:Yu,lights_phong_pars_fragment:ju,lights_physical_fragment:Ku,lights_physical_pars_fragment:Zu,lights_fragment_begin:$u,lights_fragment_maps:Ju,lights_fragment_end:Qu,logdepthbuf_fragment:td,logdepthbuf_pars_fragment:ed,logdepthbuf_pars_vertex:nd,logdepthbuf_vertex:id,map_fragment:sd,map_pars_fragment:rd,map_particle_fragment:ad,map_particle_pars_fragment:od,metalnessmap_fragment:ld,metalnessmap_pars_fragment:cd,morphinstance_vertex:hd,morphcolor_vertex:ud,morphnormal_vertex:dd,morphtarget_pars_vertex:fd,morphtarget_vertex:pd,normal_fragment_begin:md,normal_fragment_maps:gd,normal_pars_fragment:_d,normal_pars_vertex:xd,normal_vertex:vd,normalmap_pars_fragment:yd,clearcoat_normal_fragment_begin:Md,clearcoat_normal_fragment_maps:Sd,clearcoat_pars_fragment:Ed,iridescence_pars_fragment:bd,opaque_fragment:Td,packing:wd,premultiplied_alpha_fragment:Ad,project_vertex:Cd,dithering_fragment:Rd,dithering_pars_fragment:Pd,roughnessmap_fragment:Ld,roughnessmap_pars_fragment:Dd,shadowmap_pars_fragment:Id,shadowmap_pars_vertex:Ud,shadowmap_vertex:Nd,shadowmask_pars_fragment:Fd,skinbase_vertex:Od,skinning_pars_vertex:Bd,skinning_vertex:zd,skinnormal_vertex:kd,specularmap_fragment:Hd,specularmap_pars_fragment:Vd,tonemapping_fragment:Gd,tonemapping_pars_fragment:Wd,transmission_fragment:Xd,transmission_pars_fragment:qd,uv_pars_fragment:Yd,uv_pars_vertex:jd,uv_vertex:Kd,worldpos_vertex:Zd,background_vert:$d,background_frag:Jd,backgroundCube_vert:Qd,backgroundCube_frag:tf,cube_vert:ef,cube_frag:nf,depth_vert:sf,depth_frag:rf,distanceRGBA_vert:af,distanceRGBA_frag:of,equirect_vert:lf,equirect_frag:cf,linedashed_vert:hf,linedashed_frag:uf,meshbasic_vert:df,meshbasic_frag:ff,meshlambert_vert:pf,meshlambert_frag:mf,meshmatcap_vert:gf,meshmatcap_frag:_f,meshnormal_vert:xf,meshnormal_frag:vf,meshphong_vert:yf,meshphong_frag:Mf,meshphysical_vert:Sf,meshphysical_frag:Ef,meshtoon_vert:bf,meshtoon_frag:Tf,points_vert:wf,points_frag:Af,shadow_vert:Cf,shadow_frag:Rf,sprite_vert:Pf,sprite_frag:Lf},st={common:{diffuse:{value:new Ft(16777215)},opacity:{value:1},map:{value:null},mapTransform:{value:new Nt},alphaMap:{value:null},alphaMapTransform:{value:new Nt},alphaTest:{value:0}},specularmap:{specularMap:{value:null},specularMapTransform:{value:new Nt}},envmap:{envMap:{value:null},envMapRotation:{value:new Nt},flipEnvMap:{value:-1},reflectivity:{value:1},ior:{value:1.5},refractionRatio:{value:.98}},aomap:{aoMap:{value:null},aoMapIntensity:{value:1},aoMapTransform:{value:new Nt}},lightmap:{lightMap:{value:null},lightMapIntensity:{value:1},lightMapTransform:{value:new Nt}},bumpmap:{bumpMap:{value:null},bumpMapTransform:{value:new Nt},bumpScale:{value:1}},normalmap:{normalMap:{value:null},normalMapTransform:{value:new Nt},normalScale:{value:new dt(1,1)}},displacementmap:{displacementMap:{value:null},displacementMapTransform:{value:new Nt},displacementScale:{value:1},displacementBias:{value:0}},emissivemap:{emissiveMap:{value:null},emissiveMapTransform:{value:new Nt}},metalnessmap:{metalnessMap:{value:null},metalnessMapTransform:{value:new Nt}},roughnessmap:{roughnessMap:{value:null},roughnessMapTransform:{value:new Nt}},gradientmap:{gradientMap:{value:null}},fog:{fogDensity:{value:25e-5},fogNear:{value:1},fogFar:{value:2e3},fogColor:{value:new Ft(16777215)}},lights:{ambientLightColor:{value:[]},lightProbe:{value:[]},directionalLights:{value:[],properties:{direction:{},color:{}}},directionalLightShadows:{value:[],properties:{shadowIntensity:1,shadowBias:{},shadowNormalBias:{},shadowRadius:{},shadowMapSize:{}}},directionalShadowMap:{value:[]},directionalShadowMatrix:{value:[]},spotLights:{value:[],properties:{color:{},position:{},direction:{},distance:{},coneCos:{},penumbraCos:{},decay:{}}},spotLightShadows:{value:[],properties:{shadowIntensity:1,shadowBias:{},shadowNormalBias:{},shadowRadius:{},shadowMapSize:{}}},spotLightMap:{value:[]},spotShadowMap:{value:[]},spotLightMatrix:{value:[]},pointLights:{value:[],properties:{color:{},position:{},decay:{},distance:{}}},pointLightShadows:{value:[],properties:{shadowIntensity:1,shadowBias:{},shadowNormalBias:{},shadowRadius:{},shadowMapSize:{},shadowCameraNear:{},shadowCameraFar:{}}},pointShadowMap:{value:[]},pointShadowMatrix:{value:[]},hemisphereLights:{value:[],properties:{direction:{},skyColor:{},groundColor:{}}},rectAreaLights:{value:[],properties:{color:{},position:{},width:{},height:{}}},ltc_1:{value:null},ltc_2:{value:null}},points:{diffuse:{value:new Ft(16777215)},opacity:{value:1},size:{value:1},scale:{value:1},map:{value:null},alphaMap:{value:null},alphaMapTransform:{value:new Nt},alphaTest:{value:0},uvTransform:{value:new Nt}},sprite:{diffuse:{value:new Ft(16777215)},opacity:{value:1},center:{value:new dt(.5,.5)},rotation:{value:0},map:{value:null},mapTransform:{value:new Nt},alphaMap:{value:null},alphaMapTransform:{value:new Nt},alphaTest:{value:0}}},en={basic:{uniforms:Ae([st.common,st.specularmap,st.envmap,st.aomap,st.lightmap,st.fog]),vertexShader:Bt.meshbasic_vert,fragmentShader:Bt.meshbasic_frag},lambert:{uniforms:Ae([st.common,st.specularmap,st.envmap,st.aomap,st.lightmap,st.emissivemap,st.bumpmap,st.normalmap,st.displacementmap,st.fog,st.lights,{emissive:{value:new Ft(0)}}]),vertexShader:Bt.meshlambert_vert,fragmentShader:Bt.meshlambert_frag},phong:{uniforms:Ae([st.common,st.specularmap,st.envmap,st.aomap,st.lightmap,st.emissivemap,st.bumpmap,st.normalmap,st.displacementmap,st.fog,st.lights,{emissive:{value:new Ft(0)},specular:{value:new Ft(1118481)},shininess:{value:30}}]),vertexShader:Bt.meshphong_vert,fragmentShader:Bt.meshphong_frag},standard:{uniforms:Ae([st.common,st.envmap,st.aomap,st.lightmap,st.emissivemap,st.bumpmap,st.normalmap,st.displacementmap,st.roughnessmap,st.metalnessmap,st.fog,st.lights,{emissive:{value:new Ft(0)},roughness:{value:1},metalness:{value:0},envMapIntensity:{value:1}}]),vertexShader:Bt.meshphysical_vert,fragmentShader:Bt.meshphysical_frag},toon:{uniforms:Ae([st.common,st.aomap,st.lightmap,st.emissivemap,st.bumpmap,st.normalmap,st.displacementmap,st.gradientmap,st.fog,st.lights,{emissive:{value:new Ft(0)}}]),vertexShader:Bt.meshtoon_vert,fragmentShader:Bt.meshtoon_frag},matcap:{uniforms:Ae([st.common,st.bumpmap,st.normalmap,st.displacementmap,st.fog,{matcap:{value:null}}]),vertexShader:Bt.meshmatcap_vert,fragmentShader:Bt.meshmatcap_frag},points:{uniforms:Ae([st.points,st.fog]),vertexShader:Bt.points_vert,fragmentShader:Bt.points_frag},dashed:{uniforms:Ae([st.common,st.fog,{scale:{value:1},dashSize:{value:1},totalSize:{value:2}}]),vertexShader:Bt.linedashed_vert,fragmentShader:Bt.linedashed_frag},depth:{uniforms:Ae([st.common,st.displacementmap]),vertexShader:Bt.depth_vert,fragmentShader:Bt.depth_frag},normal:{uniforms:Ae([st.common,st.bumpmap,st.normalmap,st.displacementmap,{opacity:{value:1}}]),vertexShader:Bt.meshnormal_vert,fragmentShader:Bt.meshnormal_frag},sprite:{uniforms:Ae([st.sprite,st.fog]),vertexShader:Bt.sprite_vert,fragmentShader:Bt.sprite_frag},background:{uniforms:{uvTransform:{value:new Nt},t2D:{value:null},backgroundIntensity:{value:1}},vertexShader:Bt.background_vert,fragmentShader:Bt.background_frag},backgroundCube:{uniforms:{envMap:{value:null},flipEnvMap:{value:-1},backgroundBlurriness:{value:0},backgroundIntensity:{value:1},backgroundRotation:{value:new Nt}},vertexShader:Bt.backgroundCube_vert,fragmentShader:Bt.backgroundCube_frag},cube:{uniforms:{tCube:{value:null},tFlip:{value:-1},opacity:{value:1}},vertexShader:Bt.cube_vert,fragmentShader:Bt.cube_frag},equirect:{uniforms:{tEquirect:{value:null}},vertexShader:Bt.equirect_vert,fragmentShader:Bt.equirect_frag},distanceRGBA:{uniforms:Ae([st.common,st.displacementmap,{referencePosition:{value:new P},nearDistance:{value:1},farDistance:{value:1e3}}]),vertexShader:Bt.distanceRGBA_vert,fragmentShader:Bt.distanceRGBA_frag},shadow:{uniforms:Ae([st.lights,st.fog,{color:{value:new Ft(0)},opacity:{value:1}}]),vertexShader:Bt.shadow_vert,fragmentShader:Bt.shadow_frag}};en.physical={uniforms:Ae([en.standard.uniforms,{clearcoat:{value:0},clearcoatMap:{value:null},clearcoatMapTransform:{value:new Nt},clearcoatNormalMap:{value:null},clearcoatNormalMapTransform:{value:new Nt},clearcoatNormalScale:{value:new dt(1,1)},clearcoatRoughness:{value:0},clearcoatRoughnessMap:{value:null},clearcoatRoughnessMapTransform:{value:new Nt},dispersion:{value:0},iridescence:{value:0},iridescenceMap:{value:null},iridescenceMapTransform:{value:new Nt},iridescenceIOR:{value:1.3},iridescenceThicknessMinimum:{value:100},iridescenceThicknessMaximum:{value:400},iridescenceThicknessMap:{value:null},iridescenceThicknessMapTransform:{value:new Nt},sheen:{value:0},sheenColor:{value:new Ft(0)},sheenColorMap:{value:null},sheenColorMapTransform:{value:new Nt},sheenRoughness:{value:1},sheenRoughnessMap:{value:null},sheenRoughnessMapTransform:{value:new Nt},transmission:{value:0},transmissionMap:{value:null},transmissionMapTransform:{value:new Nt},transmissionSamplerSize:{value:new dt},transmissionSamplerMap:{value:null},thickness:{value:0},thicknessMap:{value:null},thicknessMapTransform:{value:new Nt},attenuationDistance:{value:0},attenuationColor:{value:new Ft(0)},specularColor:{value:new Ft(1,1,1)},specularColorMap:{value:null},specularColorMapTransform:{value:new Nt},specularIntensity:{value:1},specularIntensityMap:{value:null},specularIntensityMapTransform:{value:new Nt},anisotropyVector:{value:new dt},anisotropyMap:{value:null},anisotropyMapTransform:{value:new Nt}}]),vertexShader:Bt.meshphysical_vert,fragmentShader:Bt.meshphysical_frag};const bs={r:0,b:0,g:0},zn=new cn,Df=new Kt;function If(s,t,e,n,i,r,a){const o=new Ft(0);let l=r===!0?0:1,c,h,d=null,u=0,p=null;function g(b){let S=b.isScene===!0?b.background:null;return S&&S.isTexture&&(S=(b.backgroundBlurriness>0?e:t).get(S)),S}function v(b){let S=!1;const w=g(b);w===null?f(o,l):w&&w.isColor&&(f(w,1),S=!0);const A=s.xr.getEnvironmentBlendMode();A==="additive"?n.buffers.color.setClear(0,0,0,1,a):A==="alpha-blend"&&n.buffers.color.setClear(0,0,0,0,a),(s.autoClear||S)&&(n.buffers.depth.setTest(!0),n.buffers.depth.setMask(!0),n.buffers.color.setMask(!0),s.clear(s.autoClearColor,s.autoClearDepth,s.autoClearStencil))}function m(b,S){const w=g(S);w&&(w.isCubeTexture||w.mapping===Vs)?(h===void 0&&(h=new Me(new De(1,1,1),new Ce({name:"BackgroundCubeMaterial",uniforms:bi(en.backgroundCube.uniforms),vertexShader:en.backgroundCube.vertexShader,fragmentShader:en.backgroundCube.fragmentShader,side:Ie,depthTest:!1,depthWrite:!1,fog:!1,allowOverride:!1})),h.geometry.deleteAttribute("normal"),h.geometry.deleteAttribute("uv"),h.onBeforeRender=function(A,C,D){this.matrixWorld.copyPosition(D.matrixWorld)},Object.defineProperty(h.material,"envMap",{get:function(){return this.uniforms.envMap.value}}),i.update(h)),zn.copy(S.backgroundRotation),zn.x*=-1,zn.y*=-1,zn.z*=-1,w.isCubeTexture&&w.isRenderTargetTexture===!1&&(zn.y*=-1,zn.z*=-1),h.material.uniforms.envMap.value=w,h.material.uniforms.flipEnvMap.value=w.isCubeTexture&&w.isRenderTargetTexture===!1?-1:1,h.material.uniforms.backgroundBlurriness.value=S.backgroundBlurriness,h.material.uniforms.backgroundIntensity.value=S.backgroundIntensity,h.material.uniforms.backgroundRotation.value.setFromMatrix4(Df.makeRotationFromEuler(zn)),h.material.toneMapped=Gt.getTransfer(w.colorSpace)!==jt,(d!==w||u!==w.version||p!==s.toneMapping)&&(h.material.needsUpdate=!0,d=w,u=w.version,p=s.toneMapping),h.layers.enableAll(),b.unshift(h,h.geometry,h.material,0,0,null)):w&&w.isTexture&&(c===void 0&&(c=new Me(new Ji(2,2),new Ce({name:"BackgroundMaterial",uniforms:bi(en.background.uniforms),vertexShader:en.background.vertexShader,fragmentShader:en.background.fragmentShader,side:Dn,depthTest:!1,depthWrite:!1,fog:!1,allowOverride:!1})),c.geometry.deleteAttribute("normal"),Object.defineProperty(c.material,"map",{get:function(){return this.uniforms.t2D.value}}),i.update(c)),c.material.uniforms.t2D.value=w,c.material.uniforms.backgroundIntensity.value=S.backgroundIntensity,c.material.toneMapped=Gt.getTransfer(w.colorSpace)!==jt,w.matrixAutoUpdate===!0&&w.updateMatrix(),c.material.uniforms.uvTransform.value.copy(w.matrix),(d!==w||u!==w.version||p!==s.toneMapping)&&(c.material.needsUpdate=!0,d=w,u=w.version,p=s.toneMapping),c.layers.enableAll(),b.unshift(c,c.geometry,c.material,0,0,null))}function f(b,S){b.getRGB(bs,Bl(s)),n.buffers.color.setClear(bs.r,bs.g,bs.b,S,a)}function E(){h!==void 0&&(h.geometry.dispose(),h.material.dispose(),h=void 0),c!==void 0&&(c.geometry.dispose(),c.material.dispose(),c=void 0)}return{getClearColor:function(){return o},setClearColor:function(b,S=1){o.set(b),l=S,f(o,l)},getClearAlpha:function(){return l},setClearAlpha:function(b){l=b,f(o,l)},render:v,addToRenderList:m,dispose:E}}function Uf(s,t){const e=s.getParameter(s.MAX_VERTEX_ATTRIBS),n={},i=u(null);let r=i,a=!1;function o(x,L,N,k,X){let W=!1;const q=d(k,N,L);r!==q&&(r=q,c(r.object)),W=p(x,k,N,X),W&&g(x,k,N,X),X!==null&&t.update(X,s.ELEMENT_ARRAY_BUFFER),(W||a)&&(a=!1,S(x,L,N,k),X!==null&&s.bindBuffer(s.ELEMENT_ARRAY_BUFFER,t.get(X).buffer))}function l(){return s.createVertexArray()}function c(x){return s.bindVertexArray(x)}function h(x){return s.deleteVertexArray(x)}function d(x,L,N){const k=N.wireframe===!0;let X=n[x.id];X===void 0&&(X={},n[x.id]=X);let W=X[L.id];W===void 0&&(W={},X[L.id]=W);let q=W[k];return q===void 0&&(q=u(l()),W[k]=q),q}function u(x){const L=[],N=[],k=[];for(let X=0;X<e;X++)L[X]=0,N[X]=0,k[X]=0;return{geometry:null,program:null,wireframe:!1,newAttributes:L,enabledAttributes:N,attributeDivisors:k,object:x,attributes:{},index:null}}function p(x,L,N,k){const X=r.attributes,W=L.attributes;let q=0;const Z=N.getAttributes();for(const H in Z)if(Z[H].location>=0){const ct=X[H];let Et=W[H];if(Et===void 0&&(H==="instanceMatrix"&&x.instanceMatrix&&(Et=x.instanceMatrix),H==="instanceColor"&&x.instanceColor&&(Et=x.instanceColor)),ct===void 0||ct.attribute!==Et||Et&&ct.data!==Et.data)return!0;q++}return r.attributesNum!==q||r.index!==k}function g(x,L,N,k){const X={},W=L.attributes;let q=0;const Z=N.getAttributes();for(const H in Z)if(Z[H].location>=0){let ct=W[H];ct===void 0&&(H==="instanceMatrix"&&x.instanceMatrix&&(ct=x.instanceMatrix),H==="instanceColor"&&x.instanceColor&&(ct=x.instanceColor));const Et={};Et.attribute=ct,ct&&ct.data&&(Et.data=ct.data),X[H]=Et,q++}r.attributes=X,r.attributesNum=q,r.index=k}function v(){const x=r.newAttributes;for(let L=0,N=x.length;L<N;L++)x[L]=0}function m(x){f(x,0)}function f(x,L){const N=r.newAttributes,k=r.enabledAttributes,X=r.attributeDivisors;N[x]=1,k[x]===0&&(s.enableVertexAttribArray(x),k[x]=1),X[x]!==L&&(s.vertexAttribDivisor(x,L),X[x]=L)}function E(){const x=r.newAttributes,L=r.enabledAttributes;for(let N=0,k=L.length;N<k;N++)L[N]!==x[N]&&(s.disableVertexAttribArray(N),L[N]=0)}function b(x,L,N,k,X,W,q){q===!0?s.vertexAttribIPointer(x,L,N,X,W):s.vertexAttribPointer(x,L,N,k,X,W)}function S(x,L,N,k){v();const X=k.attributes,W=N.getAttributes(),q=L.defaultAttributeValues;for(const Z in W){const H=W[Z];if(H.location>=0){let rt=X[Z];if(rt===void 0&&(Z==="instanceMatrix"&&x.instanceMatrix&&(rt=x.instanceMatrix),Z==="instanceColor"&&x.instanceColor&&(rt=x.instanceColor)),rt!==void 0){const ct=rt.normalized,Et=rt.itemSize,kt=t.get(rt);if(kt===void 0)continue;const te=kt.buffer,se=kt.type,qt=kt.bytesPerElement,Y=se===s.INT||se===s.UNSIGNED_INT||rt.gpuType===Ea;if(rt.isInterleavedBufferAttribute){const $=rt.data,ft=$.stride,Pt=rt.offset;if($.isInstancedInterleavedBuffer){for(let St=0;St<H.locationSize;St++)f(H.location+St,$.meshPerAttribute);x.isInstancedMesh!==!0&&k._maxInstanceCount===void 0&&(k._maxInstanceCount=$.meshPerAttribute*$.count)}else for(let St=0;St<H.locationSize;St++)m(H.location+St);s.bindBuffer(s.ARRAY_BUFFER,te);for(let St=0;St<H.locationSize;St++)b(H.location+St,Et/H.locationSize,se,ct,ft*qt,(Pt+Et/H.locationSize*St)*qt,Y)}else{if(rt.isInstancedBufferAttribute){for(let $=0;$<H.locationSize;$++)f(H.location+$,rt.meshPerAttribute);x.isInstancedMesh!==!0&&k._maxInstanceCount===void 0&&(k._maxInstanceCount=rt.meshPerAttribute*rt.count)}else for(let $=0;$<H.locationSize;$++)m(H.location+$);s.bindBuffer(s.ARRAY_BUFFER,te);for(let $=0;$<H.locationSize;$++)b(H.location+$,Et/H.locationSize,se,ct,Et*qt,Et/H.locationSize*$*qt,Y)}}else if(q!==void 0){const ct=q[Z];if(ct!==void 0)switch(ct.length){case 2:s.vertexAttrib2fv(H.location,ct);break;case 3:s.vertexAttrib3fv(H.location,ct);break;case 4:s.vertexAttrib4fv(H.location,ct);break;default:s.vertexAttrib1fv(H.location,ct)}}}}E()}function w(){D();for(const x in n){const L=n[x];for(const N in L){const k=L[N];for(const X in k)h(k[X].object),delete k[X];delete L[N]}delete n[x]}}function A(x){if(n[x.id]===void 0)return;const L=n[x.id];for(const N in L){const k=L[N];for(const X in k)h(k[X].object),delete k[X];delete L[N]}delete n[x.id]}function C(x){for(const L in n){const N=n[L];if(N[x.id]===void 0)continue;const k=N[x.id];for(const X in k)h(k[X].object),delete k[X];delete N[x.id]}}function D(){y(),a=!0,r!==i&&(r=i,c(r.object))}function y(){i.geometry=null,i.program=null,i.wireframe=!1}return{setup:o,reset:D,resetDefaultState:y,dispose:w,releaseStatesOfGeometry:A,releaseStatesOfProgram:C,initAttributes:v,enableAttribute:m,disableUnusedAttributes:E}}function Nf(s,t,e){let n;function i(c){n=c}function r(c,h){s.drawArrays(n,c,h),e.update(h,n,1)}function a(c,h,d){d!==0&&(s.drawArraysInstanced(n,c,h,d),e.update(h,n,d))}function o(c,h,d){if(d===0)return;t.get("WEBGL_multi_draw").multiDrawArraysWEBGL(n,c,0,h,0,d);let p=0;for(let g=0;g<d;g++)p+=h[g];e.update(p,n,1)}function l(c,h,d,u){if(d===0)return;const p=t.get("WEBGL_multi_draw");if(p===null)for(let g=0;g<c.length;g++)a(c[g],h[g],u[g]);else{p.multiDrawArraysInstancedWEBGL(n,c,0,h,0,u,0,d);let g=0;for(let v=0;v<d;v++)g+=h[v]*u[v];e.update(g,n,1)}}this.setMode=i,this.render=r,this.renderInstances=a,this.renderMultiDraw=o,this.renderMultiDrawInstances=l}function Ff(s,t,e,n){let i;function r(){if(i!==void 0)return i;if(t.has("EXT_texture_filter_anisotropic")===!0){const C=t.get("EXT_texture_filter_anisotropic");i=s.getParameter(C.MAX_TEXTURE_MAX_ANISOTROPY_EXT)}else i=0;return i}function a(C){return!(C!==Ze&&n.convert(C)!==s.getParameter(s.IMPLEMENTATION_COLOR_READ_FORMAT))}function o(C){const D=C===yn&&(t.has("EXT_color_buffer_half_float")||t.has("EXT_color_buffer_float"));return!(C!==ln&&n.convert(C)!==s.getParameter(s.IMPLEMENTATION_COLOR_READ_TYPE)&&C!==an&&!D)}function l(C){if(C==="highp"){if(s.getShaderPrecisionFormat(s.VERTEX_SHADER,s.HIGH_FLOAT).precision>0&&s.getShaderPrecisionFormat(s.FRAGMENT_SHADER,s.HIGH_FLOAT).precision>0)return"highp";C="mediump"}return C==="mediump"&&s.getShaderPrecisionFormat(s.VERTEX_SHADER,s.MEDIUM_FLOAT).precision>0&&s.getShaderPrecisionFormat(s.FRAGMENT_SHADER,s.MEDIUM_FLOAT).precision>0?"mediump":"lowp"}let c=e.precision!==void 0?e.precision:"highp";const h=l(c);h!==c&&(console.warn("THREE.WebGLRenderer:",c,"not supported, using",h,"instead."),c=h);const d=e.logarithmicDepthBuffer===!0,u=e.reversedDepthBuffer===!0&&t.has("EXT_clip_control"),p=s.getParameter(s.MAX_TEXTURE_IMAGE_UNITS),g=s.getParameter(s.MAX_VERTEX_TEXTURE_IMAGE_UNITS),v=s.getParameter(s.MAX_TEXTURE_SIZE),m=s.getParameter(s.MAX_CUBE_MAP_TEXTURE_SIZE),f=s.getParameter(s.MAX_VERTEX_ATTRIBS),E=s.getParameter(s.MAX_VERTEX_UNIFORM_VECTORS),b=s.getParameter(s.MAX_VARYING_VECTORS),S=s.getParameter(s.MAX_FRAGMENT_UNIFORM_VECTORS),w=g>0,A=s.getParameter(s.MAX_SAMPLES);return{isWebGL2:!0,getMaxAnisotropy:r,getMaxPrecision:l,textureFormatReadable:a,textureTypeReadable:o,precision:c,logarithmicDepthBuffer:d,reversedDepthBuffer:u,maxTextures:p,maxVertexTextures:g,maxTextureSize:v,maxCubemapSize:m,maxAttributes:f,maxVertexUniforms:E,maxVaryings:b,maxFragmentUniforms:S,vertexTextures:w,maxSamples:A}}function Of(s){const t=this;let e=null,n=0,i=!1,r=!1;const a=new Cn,o=new Nt,l={value:null,needsUpdate:!1};this.uniform=l,this.numPlanes=0,this.numIntersection=0,this.init=function(d,u){const p=d.length!==0||u||n!==0||i;return i=u,n=d.length,p},this.beginShadows=function(){r=!0,h(null)},this.endShadows=function(){r=!1},this.setGlobalState=function(d,u){e=h(d,u,0)},this.setState=function(d,u,p){const g=d.clippingPlanes,v=d.clipIntersection,m=d.clipShadows,f=s.get(d);if(!i||g===null||g.length===0||r&&!m)r?h(null):c();else{const E=r?0:n,b=E*4;let S=f.clippingState||null;l.value=S,S=h(g,u,b,p);for(let w=0;w!==b;++w)S[w]=e[w];f.clippingState=S,this.numIntersection=v?this.numPlanes:0,this.numPlanes+=E}};function c(){l.value!==e&&(l.value=e,l.needsUpdate=n>0),t.numPlanes=n,t.numIntersection=0}function h(d,u,p,g){const v=d!==null?d.length:0;let m=null;if(v!==0){if(m=l.value,g!==!0||m===null){const f=p+v*4,E=u.matrixWorldInverse;o.getNormalMatrix(E),(m===null||m.length<f)&&(m=new Float32Array(f));for(let b=0,S=p;b!==v;++b,S+=4)a.copy(d[b]).applyMatrix4(E,o),a.normal.toArray(m,S),m[S+3]=a.constant}l.value=m,l.needsUpdate=!0}return t.numPlanes=v,t.numIntersection=0,m}}function Bf(s){let t=new WeakMap;function e(a,o){return o===kr?a.mapping=Mi:o===Hr&&(a.mapping=Si),a}function n(a){if(a&&a.isTexture){const o=a.mapping;if(o===kr||o===Hr)if(t.has(a)){const l=t.get(a).texture;return e(l,a.mapping)}else{const l=a.image;if(l&&l.height>0){const c=new bh(l.height);return c.fromEquirectangularTexture(s,a),t.set(a,c),a.addEventListener("dispose",i),e(c.texture,a.mapping)}else return null}}return a}function i(a){const o=a.target;o.removeEventListener("dispose",i);const l=t.get(o);l!==void 0&&(t.delete(o),l.dispose())}function r(){t=new WeakMap}return{get:n,dispose:r}}const _i=4,Oo=[.125,.215,.35,.446,.526,.582],Gn=20,yr=new Gs,Bo=new Ft;let Mr=null,Sr=0,Er=0,br=!1;const Hn=(1+Math.sqrt(5))/2,mi=1/Hn,zo=[new P(-Hn,mi,0),new P(Hn,mi,0),new P(-mi,0,Hn),new P(mi,0,Hn),new P(0,Hn,-mi),new P(0,Hn,mi),new P(-1,1,-1),new P(1,1,-1),new P(-1,1,1),new P(1,1,1)],zf=new P;class ko{constructor(t){this._renderer=t,this._pingPongRenderTarget=null,this._lodMax=0,this._cubeSize=0,this._lodPlanes=[],this._sizeLods=[],this._sigmas=[],this._blurMaterial=null,this._cubemapMaterial=null,this._equirectMaterial=null,this._compileMaterial(this._blurMaterial)}fromScene(t,e=0,n=.1,i=100,r={}){const{size:a=256,position:o=zf}=r;Mr=this._renderer.getRenderTarget(),Sr=this._renderer.getActiveCubeFace(),Er=this._renderer.getActiveMipmapLevel(),br=this._renderer.xr.enabled,this._renderer.xr.enabled=!1,this._setSize(a);const l=this._allocateTargets();return l.depthBuffer=!0,this._sceneToCubeUV(t,n,i,l,o),e>0&&this._blur(l,0,0,e),this._applyPMREM(l),this._cleanup(l),l}fromEquirectangular(t,e=null){return this._fromTexture(t,e)}fromCubemap(t,e=null){return this._fromTexture(t,e)}compileCubemapShader(){this._cubemapMaterial===null&&(this._cubemapMaterial=Go(),this._compileMaterial(this._cubemapMaterial))}compileEquirectangularShader(){this._equirectMaterial===null&&(this._equirectMaterial=Vo(),this._compileMaterial(this._equirectMaterial))}dispose(){this._dispose(),this._cubemapMaterial!==null&&this._cubemapMaterial.dispose(),this._equirectMaterial!==null&&this._equirectMaterial.dispose()}_setSize(t){this._lodMax=Math.floor(Math.log2(t)),this._cubeSize=Math.pow(2,this._lodMax)}_dispose(){this._blurMaterial!==null&&this._blurMaterial.dispose(),this._pingPongRenderTarget!==null&&this._pingPongRenderTarget.dispose();for(let t=0;t<this._lodPlanes.length;t++)this._lodPlanes[t].dispose()}_cleanup(t){this._renderer.setRenderTarget(Mr,Sr,Er),this._renderer.xr.enabled=br,t.scissorTest=!1,Ts(t,0,0,t.width,t.height)}_fromTexture(t,e){t.mapping===Mi||t.mapping===Si?this._setSize(t.image.length===0?16:t.image[0].width||t.image[0].image.width):this._setSize(t.image.width/4),Mr=this._renderer.getRenderTarget(),Sr=this._renderer.getActiveCubeFace(),Er=this._renderer.getActiveMipmapLevel(),br=this._renderer.xr.enabled,this._renderer.xr.enabled=!1;const n=e||this._allocateTargets();return this._textureToCubeUV(t,n),this._applyPMREM(n),this._cleanup(n),n}_allocateTargets(){const t=3*Math.max(this._cubeSize,112),e=4*this._cubeSize,n={magFilter:rn,minFilter:rn,generateMipmaps:!1,type:yn,format:Ze,colorSpace:Ei,depthBuffer:!1},i=Ho(t,e,n);if(this._pingPongRenderTarget===null||this._pingPongRenderTarget.width!==t||this._pingPongRenderTarget.height!==e){this._pingPongRenderTarget!==null&&this._dispose(),this._pingPongRenderTarget=Ho(t,e,n);const{_lodMax:r}=this;({sizeLods:this._sizeLods,lodPlanes:this._lodPlanes,sigmas:this._sigmas}=kf(r)),this._blurMaterial=Hf(r,t,e)}return i}_compileMaterial(t){const e=new Me(this._lodPlanes[0],t);this._renderer.compile(e,yr)}_sceneToCubeUV(t,e,n,i,r){const l=new We(90,1,e,n),c=[1,-1,1,1,1,1],h=[1,1,1,-1,-1,-1],d=this._renderer,u=d.autoClear,p=d.toneMapping;d.getClearColor(Bo),d.toneMapping=Ln,d.autoClear=!1,d.state.buffers.depth.getReversed()&&(d.setRenderTarget(i),d.clearDepth(),d.setRenderTarget(null));const v=new qn({name:"PMREM.Background",side:Ie,depthWrite:!1,depthTest:!1}),m=new Me(new De,v);let f=!1;const E=t.background;E?E.isColor&&(v.color.copy(E),t.background=null,f=!0):(v.color.copy(Bo),f=!0);for(let b=0;b<6;b++){const S=b%3;S===0?(l.up.set(0,c[b],0),l.position.set(r.x,r.y,r.z),l.lookAt(r.x+h[b],r.y,r.z)):S===1?(l.up.set(0,0,c[b]),l.position.set(r.x,r.y,r.z),l.lookAt(r.x,r.y+h[b],r.z)):(l.up.set(0,c[b],0),l.position.set(r.x,r.y,r.z),l.lookAt(r.x,r.y,r.z+h[b]));const w=this._cubeSize;Ts(i,S*w,b>2?w:0,w,w),d.setRenderTarget(i),f&&d.render(m,l),d.render(t,l)}m.geometry.dispose(),m.material.dispose(),d.toneMapping=p,d.autoClear=u,t.background=E}_textureToCubeUV(t,e){const n=this._renderer,i=t.mapping===Mi||t.mapping===Si;i?(this._cubemapMaterial===null&&(this._cubemapMaterial=Go()),this._cubemapMaterial.uniforms.flipEnvMap.value=t.isRenderTargetTexture===!1?-1:1):this._equirectMaterial===null&&(this._equirectMaterial=Vo());const r=i?this._cubemapMaterial:this._equirectMaterial,a=new Me(this._lodPlanes[0],r),o=r.uniforms;o.envMap.value=t;const l=this._cubeSize;Ts(e,0,0,3*l,2*l),n.setRenderTarget(e),n.render(a,yr)}_applyPMREM(t){const e=this._renderer,n=e.autoClear;e.autoClear=!1;const i=this._lodPlanes.length;for(let r=1;r<i;r++){const a=Math.sqrt(this._sigmas[r]*this._sigmas[r]-this._sigmas[r-1]*this._sigmas[r-1]),o=zo[(i-r-1)%zo.length];this._blur(t,r-1,r,a,o)}e.autoClear=n}_blur(t,e,n,i,r){const a=this._pingPongRenderTarget;this._halfBlur(t,a,e,n,i,"latitudinal",r),this._halfBlur(a,t,n,n,i,"longitudinal",r)}_halfBlur(t,e,n,i,r,a,o){const l=this._renderer,c=this._blurMaterial;a!=="latitudinal"&&a!=="longitudinal"&&console.error("blur direction must be either latitudinal or longitudinal!");const h=3,d=new Me(this._lodPlanes[i],c),u=c.uniforms,p=this._sizeLods[n]-1,g=isFinite(r)?Math.PI/(2*p):2*Math.PI/(2*Gn-1),v=r/g,m=isFinite(r)?1+Math.floor(h*v):Gn;m>Gn&&console.warn(`sigmaRadians, ${r}, is too large and will clip, as it requested ${m} samples when the maximum is set to ${Gn}`);const f=[];let E=0;for(let C=0;C<Gn;++C){const D=C/v,y=Math.exp(-D*D/2);f.push(y),C===0?E+=y:C<m&&(E+=2*y)}for(let C=0;C<f.length;C++)f[C]=f[C]/E;u.envMap.value=t.texture,u.samples.value=m,u.weights.value=f,u.latitudinal.value=a==="latitudinal",o&&(u.poleAxis.value=o);const{_lodMax:b}=this;u.dTheta.value=g,u.mipInt.value=b-n;const S=this._sizeLods[i],w=3*S*(i>b-_i?i-b+_i:0),A=4*(this._cubeSize-S);Ts(e,w,A,3*S,2*S),l.setRenderTarget(e),l.render(d,yr)}}function kf(s){const t=[],e=[],n=[];let i=s;const r=s-_i+1+Oo.length;for(let a=0;a<r;a++){const o=Math.pow(2,i);e.push(o);let l=1/o;a>s-_i?l=Oo[a-s+_i-1]:a===0&&(l=0),n.push(l);const c=1/(o-2),h=-c,d=1+c,u=[h,h,d,h,d,d,h,h,d,d,h,d],p=6,g=6,v=3,m=2,f=1,E=new Float32Array(v*g*p),b=new Float32Array(m*g*p),S=new Float32Array(f*g*p);for(let A=0;A<p;A++){const C=A%3*2/3-1,D=A>2?0:-1,y=[C,D,0,C+2/3,D,0,C+2/3,D+1,0,C,D,0,C+2/3,D+1,0,C,D+1,0];E.set(y,v*g*A),b.set(u,m*g*A);const x=[A,A,A,A,A,A];S.set(x,f*g*A)}const w=new me;w.setAttribute("position",new Xe(E,v)),w.setAttribute("uv",new Xe(b,m)),w.setAttribute("faceIndex",new Xe(S,f)),t.push(w),i>_i&&i--}return{lodPlanes:t,sizeLods:e,sigmas:n}}function Ho(s,t,e){const n=new $e(s,t,e);return n.texture.mapping=Vs,n.texture.name="PMREM.cubeUv",n.scissorTest=!0,n}function Ts(s,t,e,n,i){s.viewport.set(t,e,n,i),s.scissor.set(t,e,n,i)}function Hf(s,t,e){const n=new Float32Array(Gn),i=new P(0,1,0);return new Ce({name:"SphericalGaussianBlur",defines:{n:Gn,CUBEUV_TEXEL_WIDTH:1/t,CUBEUV_TEXEL_HEIGHT:1/e,CUBEUV_MAX_MIP:`${s}.0`},uniforms:{envMap:{value:null},samples:{value:1},weights:{value:n},latitudinal:{value:!1},dTheta:{value:0},mipInt:{value:0},poleAxis:{value:i}},vertexShader:Ha(),fragmentShader:`

			precision mediump float;
			precision mediump int;

			varying vec3 vOutputDirection;

			uniform sampler2D envMap;
			uniform int samples;
			uniform float weights[ n ];
			uniform bool latitudinal;
			uniform float dTheta;
			uniform float mipInt;
			uniform vec3 poleAxis;

			#define ENVMAP_TYPE_CUBE_UV
			#include <cube_uv_reflection_fragment>

			vec3 getSample( float theta, vec3 axis ) {

				float cosTheta = cos( theta );
				// Rodrigues' axis-angle rotation
				vec3 sampleDirection = vOutputDirection * cosTheta
					+ cross( axis, vOutputDirection ) * sin( theta )
					+ axis * dot( axis, vOutputDirection ) * ( 1.0 - cosTheta );

				return bilinearCubeUV( envMap, sampleDirection, mipInt );

			}

			void main() {

				vec3 axis = latitudinal ? poleAxis : cross( poleAxis, vOutputDirection );

				if ( all( equal( axis, vec3( 0.0 ) ) ) ) {

					axis = vec3( vOutputDirection.z, 0.0, - vOutputDirection.x );

				}

				axis = normalize( axis );

				gl_FragColor = vec4( 0.0, 0.0, 0.0, 1.0 );
				gl_FragColor.rgb += weights[ 0 ] * getSample( 0.0, axis );

				for ( int i = 1; i < n; i++ ) {

					if ( i >= samples ) {

						break;

					}

					float theta = dTheta * float( i );
					gl_FragColor.rgb += weights[ i ] * getSample( -1.0 * theta, axis );
					gl_FragColor.rgb += weights[ i ] * getSample( theta, axis );

				}

			}
		`,blending:vn,depthTest:!1,depthWrite:!1})}function Vo(){return new Ce({name:"EquirectangularToCubeUV",uniforms:{envMap:{value:null}},vertexShader:Ha(),fragmentShader:`

			precision mediump float;
			precision mediump int;

			varying vec3 vOutputDirection;

			uniform sampler2D envMap;

			#include <common>

			void main() {

				vec3 outputDirection = normalize( vOutputDirection );
				vec2 uv = equirectUv( outputDirection );

				gl_FragColor = vec4( texture2D ( envMap, uv ).rgb, 1.0 );

			}
		`,blending:vn,depthTest:!1,depthWrite:!1})}function Go(){return new Ce({name:"CubemapToCubeUV",uniforms:{envMap:{value:null},flipEnvMap:{value:-1}},vertexShader:Ha(),fragmentShader:`

			precision mediump float;
			precision mediump int;

			uniform float flipEnvMap;

			varying vec3 vOutputDirection;

			uniform samplerCube envMap;

			void main() {

				gl_FragColor = textureCube( envMap, vec3( flipEnvMap * vOutputDirection.x, vOutputDirection.yz ) );

			}
		`,blending:vn,depthTest:!1,depthWrite:!1})}function Ha(){return`

		precision mediump float;
		precision mediump int;

		attribute float faceIndex;

		varying vec3 vOutputDirection;

		// RH coordinate system; PMREM face-indexing convention
		vec3 getDirection( vec2 uv, float face ) {

			uv = 2.0 * uv - 1.0;

			vec3 direction = vec3( uv, 1.0 );

			if ( face == 0.0 ) {

				direction = direction.zyx; // ( 1, v, u ) pos x

			} else if ( face == 1.0 ) {

				direction = direction.xzy;
				direction.xz *= -1.0; // ( -u, 1, -v ) pos y

			} else if ( face == 2.0 ) {

				direction.x *= -1.0; // ( -u, v, 1 ) pos z

			} else if ( face == 3.0 ) {

				direction = direction.zyx;
				direction.xz *= -1.0; // ( -1, v, -u ) neg x

			} else if ( face == 4.0 ) {

				direction = direction.xzy;
				direction.xy *= -1.0; // ( -u, -1, v ) neg y

			} else if ( face == 5.0 ) {

				direction.z *= -1.0; // ( u, v, -1 ) neg z

			}

			return direction;

		}

		void main() {

			vOutputDirection = getDirection( uv, faceIndex );
			gl_Position = vec4( position, 1.0 );

		}
	`}function Vf(s){let t=new WeakMap,e=null;function n(o){if(o&&o.isTexture){const l=o.mapping,c=l===kr||l===Hr,h=l===Mi||l===Si;if(c||h){let d=t.get(o);const u=d!==void 0?d.texture.pmremVersion:0;if(o.isRenderTargetTexture&&o.pmremVersion!==u)return e===null&&(e=new ko(s)),d=c?e.fromEquirectangular(o,d):e.fromCubemap(o,d),d.texture.pmremVersion=o.pmremVersion,t.set(o,d),d.texture;if(d!==void 0)return d.texture;{const p=o.image;return c&&p&&p.height>0||h&&p&&i(p)?(e===null&&(e=new ko(s)),d=c?e.fromEquirectangular(o):e.fromCubemap(o),d.texture.pmremVersion=o.pmremVersion,t.set(o,d),o.addEventListener("dispose",r),d.texture):null}}}return o}function i(o){let l=0;const c=6;for(let h=0;h<c;h++)o[h]!==void 0&&l++;return l===c}function r(o){const l=o.target;l.removeEventListener("dispose",r);const c=t.get(l);c!==void 0&&(t.delete(l),c.dispose())}function a(){t=new WeakMap,e!==null&&(e.dispose(),e=null)}return{get:n,dispose:a}}function Gf(s){const t={};function e(n){if(t[n]!==void 0)return t[n];let i;switch(n){case"WEBGL_depth_texture":i=s.getExtension("WEBGL_depth_texture")||s.getExtension("MOZ_WEBGL_depth_texture")||s.getExtension("WEBKIT_WEBGL_depth_texture");break;case"EXT_texture_filter_anisotropic":i=s.getExtension("EXT_texture_filter_anisotropic")||s.getExtension("MOZ_EXT_texture_filter_anisotropic")||s.getExtension("WEBKIT_EXT_texture_filter_anisotropic");break;case"WEBGL_compressed_texture_s3tc":i=s.getExtension("WEBGL_compressed_texture_s3tc")||s.getExtension("MOZ_WEBGL_compressed_texture_s3tc")||s.getExtension("WEBKIT_WEBGL_compressed_texture_s3tc");break;case"WEBGL_compressed_texture_pvrtc":i=s.getExtension("WEBGL_compressed_texture_pvrtc")||s.getExtension("WEBKIT_WEBGL_compressed_texture_pvrtc");break;default:i=s.getExtension(n)}return t[n]=i,i}return{has:function(n){return e(n)!==null},init:function(){e("EXT_color_buffer_float"),e("WEBGL_clip_cull_distance"),e("OES_texture_float_linear"),e("EXT_color_buffer_half_float"),e("WEBGL_multisampled_render_to_texture"),e("WEBGL_render_shared_exponent")},get:function(n){const i=e(n);return i===null&&Ki("THREE.WebGLRenderer: "+n+" extension not supported."),i}}}function Wf(s,t,e,n){const i={},r=new WeakMap;function a(d){const u=d.target;u.index!==null&&t.remove(u.index);for(const g in u.attributes)t.remove(u.attributes[g]);u.removeEventListener("dispose",a),delete i[u.id];const p=r.get(u);p&&(t.remove(p),r.delete(u)),n.releaseStatesOfGeometry(u),u.isInstancedBufferGeometry===!0&&delete u._maxInstanceCount,e.memory.geometries--}function o(d,u){return i[u.id]===!0||(u.addEventListener("dispose",a),i[u.id]=!0,e.memory.geometries++),u}function l(d){const u=d.attributes;for(const p in u)t.update(u[p],s.ARRAY_BUFFER)}function c(d){const u=[],p=d.index,g=d.attributes.position;let v=0;if(p!==null){const E=p.array;v=p.version;for(let b=0,S=E.length;b<S;b+=3){const w=E[b+0],A=E[b+1],C=E[b+2];u.push(w,A,A,C,C,w)}}else if(g!==void 0){const E=g.array;v=g.version;for(let b=0,S=E.length/3-1;b<S;b+=3){const w=b+0,A=b+1,C=b+2;u.push(w,A,A,C,C,w)}}else return;const m=new(Il(u)?Ol:Fl)(u,1);m.version=v;const f=r.get(d);f&&t.remove(f),r.set(d,m)}function h(d){const u=r.get(d);if(u){const p=d.index;p!==null&&u.version<p.version&&c(d)}else c(d);return r.get(d)}return{get:o,update:l,getWireframeAttribute:h}}function Xf(s,t,e){let n;function i(u){n=u}let r,a;function o(u){r=u.type,a=u.bytesPerElement}function l(u,p){s.drawElements(n,p,r,u*a),e.update(p,n,1)}function c(u,p,g){g!==0&&(s.drawElementsInstanced(n,p,r,u*a,g),e.update(p,n,g))}function h(u,p,g){if(g===0)return;t.get("WEBGL_multi_draw").multiDrawElementsWEBGL(n,p,0,r,u,0,g);let m=0;for(let f=0;f<g;f++)m+=p[f];e.update(m,n,1)}function d(u,p,g,v){if(g===0)return;const m=t.get("WEBGL_multi_draw");if(m===null)for(let f=0;f<u.length;f++)c(u[f]/a,p[f],v[f]);else{m.multiDrawElementsInstancedWEBGL(n,p,0,r,u,0,v,0,g);let f=0;for(let E=0;E<g;E++)f+=p[E]*v[E];e.update(f,n,1)}}this.setMode=i,this.setIndex=o,this.render=l,this.renderInstances=c,this.renderMultiDraw=h,this.renderMultiDrawInstances=d}function qf(s){const t={geometries:0,textures:0},e={frame:0,calls:0,triangles:0,points:0,lines:0};function n(r,a,o){switch(e.calls++,a){case s.TRIANGLES:e.triangles+=o*(r/3);break;case s.LINES:e.lines+=o*(r/2);break;case s.LINE_STRIP:e.lines+=o*(r-1);break;case s.LINE_LOOP:e.lines+=o*r;break;case s.POINTS:e.points+=o*r;break;default:console.error("THREE.WebGLInfo: Unknown draw mode:",a);break}}function i(){e.calls=0,e.triangles=0,e.points=0,e.lines=0}return{memory:t,render:e,programs:null,autoReset:!0,reset:i,update:n}}function Yf(s,t,e){const n=new WeakMap,i=new Jt;function r(a,o,l){const c=a.morphTargetInfluences,h=o.morphAttributes.position||o.morphAttributes.normal||o.morphAttributes.color,d=h!==void 0?h.length:0;let u=n.get(o);if(u===void 0||u.count!==d){let x=function(){D.dispose(),n.delete(o),o.removeEventListener("dispose",x)};var p=x;u!==void 0&&u.texture.dispose();const g=o.morphAttributes.position!==void 0,v=o.morphAttributes.normal!==void 0,m=o.morphAttributes.color!==void 0,f=o.morphAttributes.position||[],E=o.morphAttributes.normal||[],b=o.morphAttributes.color||[];let S=0;g===!0&&(S=1),v===!0&&(S=2),m===!0&&(S=3);let w=o.attributes.position.count*S,A=1;w>t.maxTextureSize&&(A=Math.ceil(w/t.maxTextureSize),w=t.maxTextureSize);const C=new Float32Array(w*A*4*d),D=new Ul(C,w,A,d);D.type=an,D.needsUpdate=!0;const y=S*4;for(let L=0;L<d;L++){const N=f[L],k=E[L],X=b[L],W=w*A*4*L;for(let q=0;q<N.count;q++){const Z=q*y;g===!0&&(i.fromBufferAttribute(N,q),C[W+Z+0]=i.x,C[W+Z+1]=i.y,C[W+Z+2]=i.z,C[W+Z+3]=0),v===!0&&(i.fromBufferAttribute(k,q),C[W+Z+4]=i.x,C[W+Z+5]=i.y,C[W+Z+6]=i.z,C[W+Z+7]=0),m===!0&&(i.fromBufferAttribute(X,q),C[W+Z+8]=i.x,C[W+Z+9]=i.y,C[W+Z+10]=i.z,C[W+Z+11]=X.itemSize===4?i.w:1)}}u={count:d,texture:D,size:new dt(w,A)},n.set(o,u),o.addEventListener("dispose",x)}if(a.isInstancedMesh===!0&&a.morphTexture!==null)l.getUniforms().setValue(s,"morphTexture",a.morphTexture,e);else{let g=0;for(let m=0;m<c.length;m++)g+=c[m];const v=o.morphTargetsRelative?1:1-g;l.getUniforms().setValue(s,"morphTargetBaseInfluence",v),l.getUniforms().setValue(s,"morphTargetInfluences",c)}l.getUniforms().setValue(s,"morphTargetsTexture",u.texture,e),l.getUniforms().setValue(s,"morphTargetsTextureSize",u.size)}return{update:r}}function jf(s,t,e,n){let i=new WeakMap;function r(l){const c=n.render.frame,h=l.geometry,d=t.get(l,h);if(i.get(d)!==c&&(t.update(d),i.set(d,c)),l.isInstancedMesh&&(l.hasEventListener("dispose",o)===!1&&l.addEventListener("dispose",o),i.get(l)!==c&&(e.update(l.instanceMatrix,s.ARRAY_BUFFER),l.instanceColor!==null&&e.update(l.instanceColor,s.ARRAY_BUFFER),i.set(l,c))),l.isSkinnedMesh){const u=l.skeleton;i.get(u)!==c&&(u.update(),i.set(u,c))}return d}function a(){i=new WeakMap}function o(l){const c=l.target;c.removeEventListener("dispose",o),e.remove(c.instanceMatrix),c.instanceColor!==null&&e.remove(c.instanceColor)}return{update:r,dispose:a}}const jl=new Re,Wo=new Gl(1,1),Kl=new Ul,Zl=new lh,$l=new kl,Xo=[],qo=[],Yo=new Float32Array(16),jo=new Float32Array(9),Ko=new Float32Array(4);function Ai(s,t,e){const n=s[0];if(n<=0||n>0)return s;const i=t*e;let r=Xo[i];if(r===void 0&&(r=new Float32Array(i),Xo[i]=r),t!==0){n.toArray(r,0);for(let a=1,o=0;a!==t;++a)o+=e,s[a].toArray(r,o)}return r}function ge(s,t){if(s.length!==t.length)return!1;for(let e=0,n=s.length;e<n;e++)if(s[e]!==t[e])return!1;return!0}function _e(s,t){for(let e=0,n=t.length;e<n;e++)s[e]=t[e]}function Ws(s,t){let e=qo[t];e===void 0&&(e=new Int32Array(t),qo[t]=e);for(let n=0;n!==t;++n)e[n]=s.allocateTextureUnit();return e}function Kf(s,t){const e=this.cache;e[0]!==t&&(s.uniform1f(this.addr,t),e[0]=t)}function Zf(s,t){const e=this.cache;if(t.x!==void 0)(e[0]!==t.x||e[1]!==t.y)&&(s.uniform2f(this.addr,t.x,t.y),e[0]=t.x,e[1]=t.y);else{if(ge(e,t))return;s.uniform2fv(this.addr,t),_e(e,t)}}function $f(s,t){const e=this.cache;if(t.x!==void 0)(e[0]!==t.x||e[1]!==t.y||e[2]!==t.z)&&(s.uniform3f(this.addr,t.x,t.y,t.z),e[0]=t.x,e[1]=t.y,e[2]=t.z);else if(t.r!==void 0)(e[0]!==t.r||e[1]!==t.g||e[2]!==t.b)&&(s.uniform3f(this.addr,t.r,t.g,t.b),e[0]=t.r,e[1]=t.g,e[2]=t.b);else{if(ge(e,t))return;s.uniform3fv(this.addr,t),_e(e,t)}}function Jf(s,t){const e=this.cache;if(t.x!==void 0)(e[0]!==t.x||e[1]!==t.y||e[2]!==t.z||e[3]!==t.w)&&(s.uniform4f(this.addr,t.x,t.y,t.z,t.w),e[0]=t.x,e[1]=t.y,e[2]=t.z,e[3]=t.w);else{if(ge(e,t))return;s.uniform4fv(this.addr,t),_e(e,t)}}function Qf(s,t){const e=this.cache,n=t.elements;if(n===void 0){if(ge(e,t))return;s.uniformMatrix2fv(this.addr,!1,t),_e(e,t)}else{if(ge(e,n))return;Ko.set(n),s.uniformMatrix2fv(this.addr,!1,Ko),_e(e,n)}}function tp(s,t){const e=this.cache,n=t.elements;if(n===void 0){if(ge(e,t))return;s.uniformMatrix3fv(this.addr,!1,t),_e(e,t)}else{if(ge(e,n))return;jo.set(n),s.uniformMatrix3fv(this.addr,!1,jo),_e(e,n)}}function ep(s,t){const e=this.cache,n=t.elements;if(n===void 0){if(ge(e,t))return;s.uniformMatrix4fv(this.addr,!1,t),_e(e,t)}else{if(ge(e,n))return;Yo.set(n),s.uniformMatrix4fv(this.addr,!1,Yo),_e(e,n)}}function np(s,t){const e=this.cache;e[0]!==t&&(s.uniform1i(this.addr,t),e[0]=t)}function ip(s,t){const e=this.cache;if(t.x!==void 0)(e[0]!==t.x||e[1]!==t.y)&&(s.uniform2i(this.addr,t.x,t.y),e[0]=t.x,e[1]=t.y);else{if(ge(e,t))return;s.uniform2iv(this.addr,t),_e(e,t)}}function sp(s,t){const e=this.cache;if(t.x!==void 0)(e[0]!==t.x||e[1]!==t.y||e[2]!==t.z)&&(s.uniform3i(this.addr,t.x,t.y,t.z),e[0]=t.x,e[1]=t.y,e[2]=t.z);else{if(ge(e,t))return;s.uniform3iv(this.addr,t),_e(e,t)}}function rp(s,t){const e=this.cache;if(t.x!==void 0)(e[0]!==t.x||e[1]!==t.y||e[2]!==t.z||e[3]!==t.w)&&(s.uniform4i(this.addr,t.x,t.y,t.z,t.w),e[0]=t.x,e[1]=t.y,e[2]=t.z,e[3]=t.w);else{if(ge(e,t))return;s.uniform4iv(this.addr,t),_e(e,t)}}function ap(s,t){const e=this.cache;e[0]!==t&&(s.uniform1ui(this.addr,t),e[0]=t)}function op(s,t){const e=this.cache;if(t.x!==void 0)(e[0]!==t.x||e[1]!==t.y)&&(s.uniform2ui(this.addr,t.x,t.y),e[0]=t.x,e[1]=t.y);else{if(ge(e,t))return;s.uniform2uiv(this.addr,t),_e(e,t)}}function lp(s,t){const e=this.cache;if(t.x!==void 0)(e[0]!==t.x||e[1]!==t.y||e[2]!==t.z)&&(s.uniform3ui(this.addr,t.x,t.y,t.z),e[0]=t.x,e[1]=t.y,e[2]=t.z);else{if(ge(e,t))return;s.uniform3uiv(this.addr,t),_e(e,t)}}function cp(s,t){const e=this.cache;if(t.x!==void 0)(e[0]!==t.x||e[1]!==t.y||e[2]!==t.z||e[3]!==t.w)&&(s.uniform4ui(this.addr,t.x,t.y,t.z,t.w),e[0]=t.x,e[1]=t.y,e[2]=t.z,e[3]=t.w);else{if(ge(e,t))return;s.uniform4uiv(this.addr,t),_e(e,t)}}function hp(s,t,e){const n=this.cache,i=e.allocateTextureUnit();n[0]!==i&&(s.uniform1i(this.addr,i),n[0]=i);let r;this.type===s.SAMPLER_2D_SHADOW?(Wo.compareFunction=Dl,r=Wo):r=jl,e.setTexture2D(t||r,i)}function up(s,t,e){const n=this.cache,i=e.allocateTextureUnit();n[0]!==i&&(s.uniform1i(this.addr,i),n[0]=i),e.setTexture3D(t||Zl,i)}function dp(s,t,e){const n=this.cache,i=e.allocateTextureUnit();n[0]!==i&&(s.uniform1i(this.addr,i),n[0]=i),e.setTextureCube(t||$l,i)}function fp(s,t,e){const n=this.cache,i=e.allocateTextureUnit();n[0]!==i&&(s.uniform1i(this.addr,i),n[0]=i),e.setTexture2DArray(t||Kl,i)}function pp(s){switch(s){case 5126:return Kf;case 35664:return Zf;case 35665:return $f;case 35666:return Jf;case 35674:return Qf;case 35675:return tp;case 35676:return ep;case 5124:case 35670:return np;case 35667:case 35671:return ip;case 35668:case 35672:return sp;case 35669:case 35673:return rp;case 5125:return ap;case 36294:return op;case 36295:return lp;case 36296:return cp;case 35678:case 36198:case 36298:case 36306:case 35682:return hp;case 35679:case 36299:case 36307:return up;case 35680:case 36300:case 36308:case 36293:return dp;case 36289:case 36303:case 36311:case 36292:return fp}}function mp(s,t){s.uniform1fv(this.addr,t)}function gp(s,t){const e=Ai(t,this.size,2);s.uniform2fv(this.addr,e)}function _p(s,t){const e=Ai(t,this.size,3);s.uniform3fv(this.addr,e)}function xp(s,t){const e=Ai(t,this.size,4);s.uniform4fv(this.addr,e)}function vp(s,t){const e=Ai(t,this.size,4);s.uniformMatrix2fv(this.addr,!1,e)}function yp(s,t){const e=Ai(t,this.size,9);s.uniformMatrix3fv(this.addr,!1,e)}function Mp(s,t){const e=Ai(t,this.size,16);s.uniformMatrix4fv(this.addr,!1,e)}function Sp(s,t){s.uniform1iv(this.addr,t)}function Ep(s,t){s.uniform2iv(this.addr,t)}function bp(s,t){s.uniform3iv(this.addr,t)}function Tp(s,t){s.uniform4iv(this.addr,t)}function wp(s,t){s.uniform1uiv(this.addr,t)}function Ap(s,t){s.uniform2uiv(this.addr,t)}function Cp(s,t){s.uniform3uiv(this.addr,t)}function Rp(s,t){s.uniform4uiv(this.addr,t)}function Pp(s,t,e){const n=this.cache,i=t.length,r=Ws(e,i);ge(n,r)||(s.uniform1iv(this.addr,r),_e(n,r));for(let a=0;a!==i;++a)e.setTexture2D(t[a]||jl,r[a])}function Lp(s,t,e){const n=this.cache,i=t.length,r=Ws(e,i);ge(n,r)||(s.uniform1iv(this.addr,r),_e(n,r));for(let a=0;a!==i;++a)e.setTexture3D(t[a]||Zl,r[a])}function Dp(s,t,e){const n=this.cache,i=t.length,r=Ws(e,i);ge(n,r)||(s.uniform1iv(this.addr,r),_e(n,r));for(let a=0;a!==i;++a)e.setTextureCube(t[a]||$l,r[a])}function Ip(s,t,e){const n=this.cache,i=t.length,r=Ws(e,i);ge(n,r)||(s.uniform1iv(this.addr,r),_e(n,r));for(let a=0;a!==i;++a)e.setTexture2DArray(t[a]||Kl,r[a])}function Up(s){switch(s){case 5126:return mp;case 35664:return gp;case 35665:return _p;case 35666:return xp;case 35674:return vp;case 35675:return yp;case 35676:return Mp;case 5124:case 35670:return Sp;case 35667:case 35671:return Ep;case 35668:case 35672:return bp;case 35669:case 35673:return Tp;case 5125:return wp;case 36294:return Ap;case 36295:return Cp;case 36296:return Rp;case 35678:case 36198:case 36298:case 36306:case 35682:return Pp;case 35679:case 36299:case 36307:return Lp;case 35680:case 36300:case 36308:case 36293:return Dp;case 36289:case 36303:case 36311:case 36292:return Ip}}class Np{constructor(t,e,n){this.id=t,this.addr=n,this.cache=[],this.type=e.type,this.setValue=pp(e.type)}}class Fp{constructor(t,e,n){this.id=t,this.addr=n,this.cache=[],this.type=e.type,this.size=e.size,this.setValue=Up(e.type)}}class Op{constructor(t){this.id=t,this.seq=[],this.map={}}setValue(t,e,n){const i=this.seq;for(let r=0,a=i.length;r!==a;++r){const o=i[r];o.setValue(t,e[o.id],n)}}}const Tr=/(\w+)(\])?(\[|\.)?/g;function Zo(s,t){s.seq.push(t),s.map[t.id]=t}function Bp(s,t,e){const n=s.name,i=n.length;for(Tr.lastIndex=0;;){const r=Tr.exec(n),a=Tr.lastIndex;let o=r[1];const l=r[2]==="]",c=r[3];if(l&&(o=o|0),c===void 0||c==="["&&a+2===i){Zo(e,c===void 0?new Np(o,s,t):new Fp(o,s,t));break}else{let d=e.map[o];d===void 0&&(d=new Op(o),Zo(e,d)),e=d}}}class Us{constructor(t,e){this.seq=[],this.map={};const n=t.getProgramParameter(e,t.ACTIVE_UNIFORMS);for(let i=0;i<n;++i){const r=t.getActiveUniform(e,i),a=t.getUniformLocation(e,r.name);Bp(r,a,this)}}setValue(t,e,n,i){const r=this.map[e];r!==void 0&&r.setValue(t,n,i)}setOptional(t,e,n){const i=e[n];i!==void 0&&this.setValue(t,n,i)}static upload(t,e,n,i){for(let r=0,a=e.length;r!==a;++r){const o=e[r],l=n[o.id];l.needsUpdate!==!1&&o.setValue(t,l.value,i)}}static seqWithValue(t,e){const n=[];for(let i=0,r=t.length;i!==r;++i){const a=t[i];a.id in e&&n.push(a)}return n}}function $o(s,t,e){const n=s.createShader(t);return s.shaderSource(n,e),s.compileShader(n),n}const zp=37297;let kp=0;function Hp(s,t){const e=s.split(`
`),n=[],i=Math.max(t-6,0),r=Math.min(t+6,e.length);for(let a=i;a<r;a++){const o=a+1;n.push(`${o===t?">":" "} ${o}: ${e[a]}`)}return n.join(`
`)}const Jo=new Nt;function Vp(s){Gt._getMatrix(Jo,Gt.workingColorSpace,s);const t=`mat3( ${Jo.elements.map(e=>e.toFixed(4))} )`;switch(Gt.getTransfer(s)){case Fs:return[t,"LinearTransferOETF"];case jt:return[t,"sRGBTransferOETF"];default:return console.warn("THREE.WebGLProgram: Unsupported color space: ",s),[t,"LinearTransferOETF"]}}function Qo(s,t,e){const n=s.getShaderParameter(t,s.COMPILE_STATUS),r=(s.getShaderInfoLog(t)||"").trim();if(n&&r==="")return"";const a=/ERROR: 0:(\d+)/.exec(r);if(a){const o=parseInt(a[1]);return e.toUpperCase()+`

`+r+`

`+Hp(s.getShaderSource(t),o)}else return r}function Gp(s,t){const e=Vp(t);return[`vec4 ${s}( vec4 value ) {`,`	return ${e[1]}( vec4( value.rgb * ${e[0]}, value.a ) );`,"}"].join(`
`)}function Wp(s,t){let e;switch(t){case _l:e="Linear";break;case xl:e="Reinhard";break;case vl:e="Cineon";break;case Sa:e="ACESFilmic";break;case Ml:e="AgX";break;case Sl:e="Neutral";break;case yl:e="Custom";break;default:console.warn("THREE.WebGLProgram: Unsupported toneMapping:",t),e="Linear"}return"vec3 "+s+"( vec3 color ) { return "+e+"ToneMapping( color ); }"}const ws=new P;function Xp(){Gt.getLuminanceCoefficients(ws);const s=ws.x.toFixed(4),t=ws.y.toFixed(4),e=ws.z.toFixed(4);return["float luminance( const in vec3 rgb ) {",`	const vec3 weights = vec3( ${s}, ${t}, ${e} );`,"	return dot( weights, rgb );","}"].join(`
`)}function qp(s){return[s.extensionClipCullDistance?"#extension GL_ANGLE_clip_cull_distance : require":"",s.extensionMultiDraw?"#extension GL_ANGLE_multi_draw : require":""].filter(zi).join(`
`)}function Yp(s){const t=[];for(const e in s){const n=s[e];n!==!1&&t.push("#define "+e+" "+n)}return t.join(`
`)}function jp(s,t){const e={},n=s.getProgramParameter(t,s.ACTIVE_ATTRIBUTES);for(let i=0;i<n;i++){const r=s.getActiveAttrib(t,i),a=r.name;let o=1;r.type===s.FLOAT_MAT2&&(o=2),r.type===s.FLOAT_MAT3&&(o=3),r.type===s.FLOAT_MAT4&&(o=4),e[a]={type:r.type,location:s.getAttribLocation(t,a),locationSize:o}}return e}function zi(s){return s!==""}function tl(s,t){const e=t.numSpotLightShadows+t.numSpotLightMaps-t.numSpotLightShadowsWithMaps;return s.replace(/NUM_DIR_LIGHTS/g,t.numDirLights).replace(/NUM_SPOT_LIGHTS/g,t.numSpotLights).replace(/NUM_SPOT_LIGHT_MAPS/g,t.numSpotLightMaps).replace(/NUM_SPOT_LIGHT_COORDS/g,e).replace(/NUM_RECT_AREA_LIGHTS/g,t.numRectAreaLights).replace(/NUM_POINT_LIGHTS/g,t.numPointLights).replace(/NUM_HEMI_LIGHTS/g,t.numHemiLights).replace(/NUM_DIR_LIGHT_SHADOWS/g,t.numDirLightShadows).replace(/NUM_SPOT_LIGHT_SHADOWS_WITH_MAPS/g,t.numSpotLightShadowsWithMaps).replace(/NUM_SPOT_LIGHT_SHADOWS/g,t.numSpotLightShadows).replace(/NUM_POINT_LIGHT_SHADOWS/g,t.numPointLightShadows)}function el(s,t){return s.replace(/NUM_CLIPPING_PLANES/g,t.numClippingPlanes).replace(/UNION_CLIPPING_PLANES/g,t.numClippingPlanes-t.numClipIntersection)}const Kp=/^[ \t]*#include +<([\w\d./]+)>/gm;function ya(s){return s.replace(Kp,$p)}const Zp=new Map;function $p(s,t){let e=Bt[t];if(e===void 0){const n=Zp.get(t);if(n!==void 0)e=Bt[n],console.warn('THREE.WebGLRenderer: Shader chunk "%s" has been deprecated. Use "%s" instead.',t,n);else throw new Error("Can not resolve #include <"+t+">")}return ya(e)}const Jp=/#pragma unroll_loop_start\s+for\s*\(\s*int\s+i\s*=\s*(\d+)\s*;\s*i\s*<\s*(\d+)\s*;\s*i\s*\+\+\s*\)\s*{([\s\S]+?)}\s+#pragma unroll_loop_end/g;function nl(s){return s.replace(Jp,Qp)}function Qp(s,t,e,n){let i="";for(let r=parseInt(t);r<parseInt(e);r++)i+=n.replace(/\[\s*i\s*\]/g,"[ "+r+" ]").replace(/UNROLLED_LOOP_INDEX/g,r);return i}function il(s){let t=`precision ${s.precision} float;
	precision ${s.precision} int;
	precision ${s.precision} sampler2D;
	precision ${s.precision} samplerCube;
	precision ${s.precision} sampler3D;
	precision ${s.precision} sampler2DArray;
	precision ${s.precision} sampler2DShadow;
	precision ${s.precision} samplerCubeShadow;
	precision ${s.precision} sampler2DArrayShadow;
	precision ${s.precision} isampler2D;
	precision ${s.precision} isampler3D;
	precision ${s.precision} isamplerCube;
	precision ${s.precision} isampler2DArray;
	precision ${s.precision} usampler2D;
	precision ${s.precision} usampler3D;
	precision ${s.precision} usamplerCube;
	precision ${s.precision} usampler2DArray;
	`;return s.precision==="highp"?t+=`
#define HIGH_PRECISION`:s.precision==="mediump"?t+=`
#define MEDIUM_PRECISION`:s.precision==="lowp"&&(t+=`
#define LOW_PRECISION`),t}function tm(s){let t="SHADOWMAP_TYPE_BASIC";return s.shadowMapType===pl?t="SHADOWMAP_TYPE_PCF":s.shadowMapType===ml?t="SHADOWMAP_TYPE_PCF_SOFT":s.shadowMapType===_n&&(t="SHADOWMAP_TYPE_VSM"),t}function em(s){let t="ENVMAP_TYPE_CUBE";if(s.envMap)switch(s.envMapMode){case Mi:case Si:t="ENVMAP_TYPE_CUBE";break;case Vs:t="ENVMAP_TYPE_CUBE_UV";break}return t}function nm(s){let t="ENVMAP_MODE_REFLECTION";return s.envMap&&s.envMapMode===Si&&(t="ENVMAP_MODE_REFRACTION"),t}function im(s){let t="ENVMAP_BLENDING_NONE";if(s.envMap)switch(s.combine){case gl:t="ENVMAP_BLENDING_MULTIPLY";break;case wc:t="ENVMAP_BLENDING_MIX";break;case Ac:t="ENVMAP_BLENDING_ADD";break}return t}function sm(s){const t=s.envMapCubeUVHeight;if(t===null)return null;const e=Math.log2(t)-2,n=1/t;return{texelWidth:1/(3*Math.max(Math.pow(2,e),112)),texelHeight:n,maxMip:e}}function rm(s,t,e,n){const i=s.getContext(),r=e.defines;let a=e.vertexShader,o=e.fragmentShader;const l=tm(e),c=em(e),h=nm(e),d=im(e),u=sm(e),p=qp(e),g=Yp(r),v=i.createProgram();let m,f,E=e.glslVersion?"#version "+e.glslVersion+`
`:"";e.isRawShaderMaterial?(m=["#define SHADER_TYPE "+e.shaderType,"#define SHADER_NAME "+e.shaderName,g].filter(zi).join(`
`),m.length>0&&(m+=`
`),f=["#define SHADER_TYPE "+e.shaderType,"#define SHADER_NAME "+e.shaderName,g].filter(zi).join(`
`),f.length>0&&(f+=`
`)):(m=[il(e),"#define SHADER_TYPE "+e.shaderType,"#define SHADER_NAME "+e.shaderName,g,e.extensionClipCullDistance?"#define USE_CLIP_DISTANCE":"",e.batching?"#define USE_BATCHING":"",e.batchingColor?"#define USE_BATCHING_COLOR":"",e.instancing?"#define USE_INSTANCING":"",e.instancingColor?"#define USE_INSTANCING_COLOR":"",e.instancingMorph?"#define USE_INSTANCING_MORPH":"",e.useFog&&e.fog?"#define USE_FOG":"",e.useFog&&e.fogExp2?"#define FOG_EXP2":"",e.map?"#define USE_MAP":"",e.envMap?"#define USE_ENVMAP":"",e.envMap?"#define "+h:"",e.lightMap?"#define USE_LIGHTMAP":"",e.aoMap?"#define USE_AOMAP":"",e.bumpMap?"#define USE_BUMPMAP":"",e.normalMap?"#define USE_NORMALMAP":"",e.normalMapObjectSpace?"#define USE_NORMALMAP_OBJECTSPACE":"",e.normalMapTangentSpace?"#define USE_NORMALMAP_TANGENTSPACE":"",e.displacementMap?"#define USE_DISPLACEMENTMAP":"",e.emissiveMap?"#define USE_EMISSIVEMAP":"",e.anisotropy?"#define USE_ANISOTROPY":"",e.anisotropyMap?"#define USE_ANISOTROPYMAP":"",e.clearcoatMap?"#define USE_CLEARCOATMAP":"",e.clearcoatRoughnessMap?"#define USE_CLEARCOAT_ROUGHNESSMAP":"",e.clearcoatNormalMap?"#define USE_CLEARCOAT_NORMALMAP":"",e.iridescenceMap?"#define USE_IRIDESCENCEMAP":"",e.iridescenceThicknessMap?"#define USE_IRIDESCENCE_THICKNESSMAP":"",e.specularMap?"#define USE_SPECULARMAP":"",e.specularColorMap?"#define USE_SPECULAR_COLORMAP":"",e.specularIntensityMap?"#define USE_SPECULAR_INTENSITYMAP":"",e.roughnessMap?"#define USE_ROUGHNESSMAP":"",e.metalnessMap?"#define USE_METALNESSMAP":"",e.alphaMap?"#define USE_ALPHAMAP":"",e.alphaHash?"#define USE_ALPHAHASH":"",e.transmission?"#define USE_TRANSMISSION":"",e.transmissionMap?"#define USE_TRANSMISSIONMAP":"",e.thicknessMap?"#define USE_THICKNESSMAP":"",e.sheenColorMap?"#define USE_SHEEN_COLORMAP":"",e.sheenRoughnessMap?"#define USE_SHEEN_ROUGHNESSMAP":"",e.mapUv?"#define MAP_UV "+e.mapUv:"",e.alphaMapUv?"#define ALPHAMAP_UV "+e.alphaMapUv:"",e.lightMapUv?"#define LIGHTMAP_UV "+e.lightMapUv:"",e.aoMapUv?"#define AOMAP_UV "+e.aoMapUv:"",e.emissiveMapUv?"#define EMISSIVEMAP_UV "+e.emissiveMapUv:"",e.bumpMapUv?"#define BUMPMAP_UV "+e.bumpMapUv:"",e.normalMapUv?"#define NORMALMAP_UV "+e.normalMapUv:"",e.displacementMapUv?"#define DISPLACEMENTMAP_UV "+e.displacementMapUv:"",e.metalnessMapUv?"#define METALNESSMAP_UV "+e.metalnessMapUv:"",e.roughnessMapUv?"#define ROUGHNESSMAP_UV "+e.roughnessMapUv:"",e.anisotropyMapUv?"#define ANISOTROPYMAP_UV "+e.anisotropyMapUv:"",e.clearcoatMapUv?"#define CLEARCOATMAP_UV "+e.clearcoatMapUv:"",e.clearcoatNormalMapUv?"#define CLEARCOAT_NORMALMAP_UV "+e.clearcoatNormalMapUv:"",e.clearcoatRoughnessMapUv?"#define CLEARCOAT_ROUGHNESSMAP_UV "+e.clearcoatRoughnessMapUv:"",e.iridescenceMapUv?"#define IRIDESCENCEMAP_UV "+e.iridescenceMapUv:"",e.iridescenceThicknessMapUv?"#define IRIDESCENCE_THICKNESSMAP_UV "+e.iridescenceThicknessMapUv:"",e.sheenColorMapUv?"#define SHEEN_COLORMAP_UV "+e.sheenColorMapUv:"",e.sheenRoughnessMapUv?"#define SHEEN_ROUGHNESSMAP_UV "+e.sheenRoughnessMapUv:"",e.specularMapUv?"#define SPECULARMAP_UV "+e.specularMapUv:"",e.specularColorMapUv?"#define SPECULAR_COLORMAP_UV "+e.specularColorMapUv:"",e.specularIntensityMapUv?"#define SPECULAR_INTENSITYMAP_UV "+e.specularIntensityMapUv:"",e.transmissionMapUv?"#define TRANSMISSIONMAP_UV "+e.transmissionMapUv:"",e.thicknessMapUv?"#define THICKNESSMAP_UV "+e.thicknessMapUv:"",e.vertexTangents&&e.flatShading===!1?"#define USE_TANGENT":"",e.vertexColors?"#define USE_COLOR":"",e.vertexAlphas?"#define USE_COLOR_ALPHA":"",e.vertexUv1s?"#define USE_UV1":"",e.vertexUv2s?"#define USE_UV2":"",e.vertexUv3s?"#define USE_UV3":"",e.pointsUvs?"#define USE_POINTS_UV":"",e.flatShading?"#define FLAT_SHADED":"",e.skinning?"#define USE_SKINNING":"",e.morphTargets?"#define USE_MORPHTARGETS":"",e.morphNormals&&e.flatShading===!1?"#define USE_MORPHNORMALS":"",e.morphColors?"#define USE_MORPHCOLORS":"",e.morphTargetsCount>0?"#define MORPHTARGETS_TEXTURE_STRIDE "+e.morphTextureStride:"",e.morphTargetsCount>0?"#define MORPHTARGETS_COUNT "+e.morphTargetsCount:"",e.doubleSided?"#define DOUBLE_SIDED":"",e.flipSided?"#define FLIP_SIDED":"",e.shadowMapEnabled?"#define USE_SHADOWMAP":"",e.shadowMapEnabled?"#define "+l:"",e.sizeAttenuation?"#define USE_SIZEATTENUATION":"",e.numLightProbes>0?"#define USE_LIGHT_PROBES":"",e.logarithmicDepthBuffer?"#define USE_LOGARITHMIC_DEPTH_BUFFER":"",e.reversedDepthBuffer?"#define USE_REVERSED_DEPTH_BUFFER":"","uniform mat4 modelMatrix;","uniform mat4 modelViewMatrix;","uniform mat4 projectionMatrix;","uniform mat4 viewMatrix;","uniform mat3 normalMatrix;","uniform vec3 cameraPosition;","uniform bool isOrthographic;","#ifdef USE_INSTANCING","	attribute mat4 instanceMatrix;","#endif","#ifdef USE_INSTANCING_COLOR","	attribute vec3 instanceColor;","#endif","#ifdef USE_INSTANCING_MORPH","	uniform sampler2D morphTexture;","#endif","attribute vec3 position;","attribute vec3 normal;","attribute vec2 uv;","#ifdef USE_UV1","	attribute vec2 uv1;","#endif","#ifdef USE_UV2","	attribute vec2 uv2;","#endif","#ifdef USE_UV3","	attribute vec2 uv3;","#endif","#ifdef USE_TANGENT","	attribute vec4 tangent;","#endif","#if defined( USE_COLOR_ALPHA )","	attribute vec4 color;","#elif defined( USE_COLOR )","	attribute vec3 color;","#endif","#ifdef USE_SKINNING","	attribute vec4 skinIndex;","	attribute vec4 skinWeight;","#endif",`
`].filter(zi).join(`
`),f=[il(e),"#define SHADER_TYPE "+e.shaderType,"#define SHADER_NAME "+e.shaderName,g,e.useFog&&e.fog?"#define USE_FOG":"",e.useFog&&e.fogExp2?"#define FOG_EXP2":"",e.alphaToCoverage?"#define ALPHA_TO_COVERAGE":"",e.map?"#define USE_MAP":"",e.matcap?"#define USE_MATCAP":"",e.envMap?"#define USE_ENVMAP":"",e.envMap?"#define "+c:"",e.envMap?"#define "+h:"",e.envMap?"#define "+d:"",u?"#define CUBEUV_TEXEL_WIDTH "+u.texelWidth:"",u?"#define CUBEUV_TEXEL_HEIGHT "+u.texelHeight:"",u?"#define CUBEUV_MAX_MIP "+u.maxMip+".0":"",e.lightMap?"#define USE_LIGHTMAP":"",e.aoMap?"#define USE_AOMAP":"",e.bumpMap?"#define USE_BUMPMAP":"",e.normalMap?"#define USE_NORMALMAP":"",e.normalMapObjectSpace?"#define USE_NORMALMAP_OBJECTSPACE":"",e.normalMapTangentSpace?"#define USE_NORMALMAP_TANGENTSPACE":"",e.emissiveMap?"#define USE_EMISSIVEMAP":"",e.anisotropy?"#define USE_ANISOTROPY":"",e.anisotropyMap?"#define USE_ANISOTROPYMAP":"",e.clearcoat?"#define USE_CLEARCOAT":"",e.clearcoatMap?"#define USE_CLEARCOATMAP":"",e.clearcoatRoughnessMap?"#define USE_CLEARCOAT_ROUGHNESSMAP":"",e.clearcoatNormalMap?"#define USE_CLEARCOAT_NORMALMAP":"",e.dispersion?"#define USE_DISPERSION":"",e.iridescence?"#define USE_IRIDESCENCE":"",e.iridescenceMap?"#define USE_IRIDESCENCEMAP":"",e.iridescenceThicknessMap?"#define USE_IRIDESCENCE_THICKNESSMAP":"",e.specularMap?"#define USE_SPECULARMAP":"",e.specularColorMap?"#define USE_SPECULAR_COLORMAP":"",e.specularIntensityMap?"#define USE_SPECULAR_INTENSITYMAP":"",e.roughnessMap?"#define USE_ROUGHNESSMAP":"",e.metalnessMap?"#define USE_METALNESSMAP":"",e.alphaMap?"#define USE_ALPHAMAP":"",e.alphaTest?"#define USE_ALPHATEST":"",e.alphaHash?"#define USE_ALPHAHASH":"",e.sheen?"#define USE_SHEEN":"",e.sheenColorMap?"#define USE_SHEEN_COLORMAP":"",e.sheenRoughnessMap?"#define USE_SHEEN_ROUGHNESSMAP":"",e.transmission?"#define USE_TRANSMISSION":"",e.transmissionMap?"#define USE_TRANSMISSIONMAP":"",e.thicknessMap?"#define USE_THICKNESSMAP":"",e.vertexTangents&&e.flatShading===!1?"#define USE_TANGENT":"",e.vertexColors||e.instancingColor||e.batchingColor?"#define USE_COLOR":"",e.vertexAlphas?"#define USE_COLOR_ALPHA":"",e.vertexUv1s?"#define USE_UV1":"",e.vertexUv2s?"#define USE_UV2":"",e.vertexUv3s?"#define USE_UV3":"",e.pointsUvs?"#define USE_POINTS_UV":"",e.gradientMap?"#define USE_GRADIENTMAP":"",e.flatShading?"#define FLAT_SHADED":"",e.doubleSided?"#define DOUBLE_SIDED":"",e.flipSided?"#define FLIP_SIDED":"",e.shadowMapEnabled?"#define USE_SHADOWMAP":"",e.shadowMapEnabled?"#define "+l:"",e.premultipliedAlpha?"#define PREMULTIPLIED_ALPHA":"",e.numLightProbes>0?"#define USE_LIGHT_PROBES":"",e.decodeVideoTexture?"#define DECODE_VIDEO_TEXTURE":"",e.decodeVideoTextureEmissive?"#define DECODE_VIDEO_TEXTURE_EMISSIVE":"",e.logarithmicDepthBuffer?"#define USE_LOGARITHMIC_DEPTH_BUFFER":"",e.reversedDepthBuffer?"#define USE_REVERSED_DEPTH_BUFFER":"","uniform mat4 viewMatrix;","uniform vec3 cameraPosition;","uniform bool isOrthographic;",e.toneMapping!==Ln?"#define TONE_MAPPING":"",e.toneMapping!==Ln?Bt.tonemapping_pars_fragment:"",e.toneMapping!==Ln?Wp("toneMapping",e.toneMapping):"",e.dithering?"#define DITHERING":"",e.opaque?"#define OPAQUE":"",Bt.colorspace_pars_fragment,Gp("linearToOutputTexel",e.outputColorSpace),Xp(),e.useDepthPacking?"#define DEPTH_PACKING "+e.depthPacking:"",`
`].filter(zi).join(`
`)),a=ya(a),a=tl(a,e),a=el(a,e),o=ya(o),o=tl(o,e),o=el(o,e),a=nl(a),o=nl(o),e.isRawShaderMaterial!==!0&&(E=`#version 300 es
`,m=[p,"#define attribute in","#define varying out","#define texture2D texture"].join(`
`)+`
`+m,f=["#define varying in",e.glslVersion===eo?"":"layout(location = 0) out highp vec4 pc_fragColor;",e.glslVersion===eo?"":"#define gl_FragColor pc_fragColor","#define gl_FragDepthEXT gl_FragDepth","#define texture2D texture","#define textureCube texture","#define texture2DProj textureProj","#define texture2DLodEXT textureLod","#define texture2DProjLodEXT textureProjLod","#define textureCubeLodEXT textureLod","#define texture2DGradEXT textureGrad","#define texture2DProjGradEXT textureProjGrad","#define textureCubeGradEXT textureGrad"].join(`
`)+`
`+f);const b=E+m+a,S=E+f+o,w=$o(i,i.VERTEX_SHADER,b),A=$o(i,i.FRAGMENT_SHADER,S);i.attachShader(v,w),i.attachShader(v,A),e.index0AttributeName!==void 0?i.bindAttribLocation(v,0,e.index0AttributeName):e.morphTargets===!0&&i.bindAttribLocation(v,0,"position"),i.linkProgram(v);function C(L){if(s.debug.checkShaderErrors){const N=i.getProgramInfoLog(v)||"",k=i.getShaderInfoLog(w)||"",X=i.getShaderInfoLog(A)||"",W=N.trim(),q=k.trim(),Z=X.trim();let H=!0,rt=!0;if(i.getProgramParameter(v,i.LINK_STATUS)===!1)if(H=!1,typeof s.debug.onShaderError=="function")s.debug.onShaderError(i,v,w,A);else{const ct=Qo(i,w,"vertex"),Et=Qo(i,A,"fragment");console.error("THREE.WebGLProgram: Shader Error "+i.getError()+" - VALIDATE_STATUS "+i.getProgramParameter(v,i.VALIDATE_STATUS)+`

Material Name: `+L.name+`
Material Type: `+L.type+`

Program Info Log: `+W+`
`+ct+`
`+Et)}else W!==""?console.warn("THREE.WebGLProgram: Program Info Log:",W):(q===""||Z==="")&&(rt=!1);rt&&(L.diagnostics={runnable:H,programLog:W,vertexShader:{log:q,prefix:m},fragmentShader:{log:Z,prefix:f}})}i.deleteShader(w),i.deleteShader(A),D=new Us(i,v),y=jp(i,v)}let D;this.getUniforms=function(){return D===void 0&&C(this),D};let y;this.getAttributes=function(){return y===void 0&&C(this),y};let x=e.rendererExtensionParallelShaderCompile===!1;return this.isReady=function(){return x===!1&&(x=i.getProgramParameter(v,zp)),x},this.destroy=function(){n.releaseStatesOfProgram(this),i.deleteProgram(v),this.program=void 0},this.type=e.shaderType,this.name=e.shaderName,this.id=kp++,this.cacheKey=t,this.usedTimes=1,this.program=v,this.vertexShader=w,this.fragmentShader=A,this}let am=0;class om{constructor(){this.shaderCache=new Map,this.materialCache=new Map}update(t){const e=t.vertexShader,n=t.fragmentShader,i=this._getShaderStage(e),r=this._getShaderStage(n),a=this._getShaderCacheForMaterial(t);return a.has(i)===!1&&(a.add(i),i.usedTimes++),a.has(r)===!1&&(a.add(r),r.usedTimes++),this}remove(t){const e=this.materialCache.get(t);for(const n of e)n.usedTimes--,n.usedTimes===0&&this.shaderCache.delete(n.code);return this.materialCache.delete(t),this}getVertexShaderID(t){return this._getShaderStage(t.vertexShader).id}getFragmentShaderID(t){return this._getShaderStage(t.fragmentShader).id}dispose(){this.shaderCache.clear(),this.materialCache.clear()}_getShaderCacheForMaterial(t){const e=this.materialCache;let n=e.get(t);return n===void 0&&(n=new Set,e.set(t,n)),n}_getShaderStage(t){const e=this.shaderCache;let n=e.get(t);return n===void 0&&(n=new lm(t),e.set(t,n)),n}}class lm{constructor(t){this.id=am++,this.code=t,this.usedTimes=0}}function cm(s,t,e,n,i,r,a){const o=new Da,l=new om,c=new Set,h=[],d=i.logarithmicDepthBuffer,u=i.vertexTextures;let p=i.precision;const g={MeshDepthMaterial:"depth",MeshDistanceMaterial:"distanceRGBA",MeshNormalMaterial:"normal",MeshBasicMaterial:"basic",MeshLambertMaterial:"lambert",MeshPhongMaterial:"phong",MeshToonMaterial:"toon",MeshStandardMaterial:"physical",MeshPhysicalMaterial:"physical",MeshMatcapMaterial:"matcap",LineBasicMaterial:"basic",LineDashedMaterial:"dashed",PointsMaterial:"points",ShadowMaterial:"shadow",SpriteMaterial:"sprite"};function v(y){return c.add(y),y===0?"uv":`uv${y}`}function m(y,x,L,N,k){const X=N.fog,W=k.geometry,q=y.isMeshStandardMaterial?N.environment:null,Z=(y.isMeshStandardMaterial?e:t).get(y.envMap||q),H=Z&&Z.mapping===Vs?Z.image.height:null,rt=g[y.type];y.precision!==null&&(p=i.getMaxPrecision(y.precision),p!==y.precision&&console.warn("THREE.WebGLProgram.getParameters:",y.precision,"not supported, using",p,"instead."));const ct=W.morphAttributes.position||W.morphAttributes.normal||W.morphAttributes.color,Et=ct!==void 0?ct.length:0;let kt=0;W.morphAttributes.position!==void 0&&(kt=1),W.morphAttributes.normal!==void 0&&(kt=2),W.morphAttributes.color!==void 0&&(kt=3);let te,se,qt,Y;if(rt){const Yt=en[rt];te=Yt.vertexShader,se=Yt.fragmentShader}else te=y.vertexShader,se=y.fragmentShader,l.update(y),qt=l.getVertexShaderID(y),Y=l.getFragmentShaderID(y);const $=s.getRenderTarget(),ft=s.state.buffers.depth.getReversed(),Pt=k.isInstancedMesh===!0,St=k.isBatchedMesh===!0,Vt=!!y.map,Se=!!y.matcap,R=!!Z,re=!!y.aoMap,It=!!y.lightMap,Ct=!!y.bumpMap,gt=!!y.normalMap,ae=!!y.displacementMap,_t=!!y.emissiveMap,Ot=!!y.metalnessMap,xe=!!y.roughnessMap,he=y.anisotropy>0,T=y.clearcoat>0,_=y.dispersion>0,O=y.iridescence>0,G=y.sheen>0,K=y.transmission>0,V=he&&!!y.anisotropyMap,Mt=T&&!!y.clearcoatMap,nt=T&&!!y.clearcoatNormalMap,xt=T&&!!y.clearcoatRoughnessMap,vt=O&&!!y.iridescenceMap,tt=O&&!!y.iridescenceThicknessMap,lt=G&&!!y.sheenColorMap,At=G&&!!y.sheenRoughnessMap,yt=!!y.specularMap,at=!!y.specularColorMap,Ut=!!y.specularIntensityMap,I=K&&!!y.transmissionMap,et=K&&!!y.thicknessMap,it=!!y.gradientMap,ut=!!y.alphaMap,J=y.alphaTest>0,j=!!y.alphaHash,mt=!!y.extensions;let Lt=Ln;y.toneMapped&&($===null||$.isXRRenderTarget===!0)&&(Lt=s.toneMapping);const ee={shaderID:rt,shaderType:y.type,shaderName:y.name,vertexShader:te,fragmentShader:se,defines:y.defines,customVertexShaderID:qt,customFragmentShaderID:Y,isRawShaderMaterial:y.isRawShaderMaterial===!0,glslVersion:y.glslVersion,precision:p,batching:St,batchingColor:St&&k._colorsTexture!==null,instancing:Pt,instancingColor:Pt&&k.instanceColor!==null,instancingMorph:Pt&&k.morphTexture!==null,supportsVertexTextures:u,outputColorSpace:$===null?s.outputColorSpace:$.isXRRenderTarget===!0?$.texture.colorSpace:Ei,alphaToCoverage:!!y.alphaToCoverage,map:Vt,matcap:Se,envMap:R,envMapMode:R&&Z.mapping,envMapCubeUVHeight:H,aoMap:re,lightMap:It,bumpMap:Ct,normalMap:gt,displacementMap:u&&ae,emissiveMap:_t,normalMapObjectSpace:gt&&y.normalMapType===Lc,normalMapTangentSpace:gt&&y.normalMapType===Ll,metalnessMap:Ot,roughnessMap:xe,anisotropy:he,anisotropyMap:V,clearcoat:T,clearcoatMap:Mt,clearcoatNormalMap:nt,clearcoatRoughnessMap:xt,dispersion:_,iridescence:O,iridescenceMap:vt,iridescenceThicknessMap:tt,sheen:G,sheenColorMap:lt,sheenRoughnessMap:At,specularMap:yt,specularColorMap:at,specularIntensityMap:Ut,transmission:K,transmissionMap:I,thicknessMap:et,gradientMap:it,opaque:y.transparent===!1&&y.blending===xi&&y.alphaToCoverage===!1,alphaMap:ut,alphaTest:J,alphaHash:j,combine:y.combine,mapUv:Vt&&v(y.map.channel),aoMapUv:re&&v(y.aoMap.channel),lightMapUv:It&&v(y.lightMap.channel),bumpMapUv:Ct&&v(y.bumpMap.channel),normalMapUv:gt&&v(y.normalMap.channel),displacementMapUv:ae&&v(y.displacementMap.channel),emissiveMapUv:_t&&v(y.emissiveMap.channel),metalnessMapUv:Ot&&v(y.metalnessMap.channel),roughnessMapUv:xe&&v(y.roughnessMap.channel),anisotropyMapUv:V&&v(y.anisotropyMap.channel),clearcoatMapUv:Mt&&v(y.clearcoatMap.channel),clearcoatNormalMapUv:nt&&v(y.clearcoatNormalMap.channel),clearcoatRoughnessMapUv:xt&&v(y.clearcoatRoughnessMap.channel),iridescenceMapUv:vt&&v(y.iridescenceMap.channel),iridescenceThicknessMapUv:tt&&v(y.iridescenceThicknessMap.channel),sheenColorMapUv:lt&&v(y.sheenColorMap.channel),sheenRoughnessMapUv:At&&v(y.sheenRoughnessMap.channel),specularMapUv:yt&&v(y.specularMap.channel),specularColorMapUv:at&&v(y.specularColorMap.channel),specularIntensityMapUv:Ut&&v(y.specularIntensityMap.channel),transmissionMapUv:I&&v(y.transmissionMap.channel),thicknessMapUv:et&&v(y.thicknessMap.channel),alphaMapUv:ut&&v(y.alphaMap.channel),vertexTangents:!!W.attributes.tangent&&(gt||he),vertexColors:y.vertexColors,vertexAlphas:y.vertexColors===!0&&!!W.attributes.color&&W.attributes.color.itemSize===4,pointsUvs:k.isPoints===!0&&!!W.attributes.uv&&(Vt||ut),fog:!!X,useFog:y.fog===!0,fogExp2:!!X&&X.isFogExp2,flatShading:y.flatShading===!0&&y.wireframe===!1,sizeAttenuation:y.sizeAttenuation===!0,logarithmicDepthBuffer:d,reversedDepthBuffer:ft,skinning:k.isSkinnedMesh===!0,morphTargets:W.morphAttributes.position!==void 0,morphNormals:W.morphAttributes.normal!==void 0,morphColors:W.morphAttributes.color!==void 0,morphTargetsCount:Et,morphTextureStride:kt,numDirLights:x.directional.length,numPointLights:x.point.length,numSpotLights:x.spot.length,numSpotLightMaps:x.spotLightMap.length,numRectAreaLights:x.rectArea.length,numHemiLights:x.hemi.length,numDirLightShadows:x.directionalShadowMap.length,numPointLightShadows:x.pointShadowMap.length,numSpotLightShadows:x.spotShadowMap.length,numSpotLightShadowsWithMaps:x.numSpotLightShadowsWithMaps,numLightProbes:x.numLightProbes,numClippingPlanes:a.numPlanes,numClipIntersection:a.numIntersection,dithering:y.dithering,shadowMapEnabled:s.shadowMap.enabled&&L.length>0,shadowMapType:s.shadowMap.type,toneMapping:Lt,decodeVideoTexture:Vt&&y.map.isVideoTexture===!0&&Gt.getTransfer(y.map.colorSpace)===jt,decodeVideoTextureEmissive:_t&&y.emissiveMap.isVideoTexture===!0&&Gt.getTransfer(y.emissiveMap.colorSpace)===jt,premultipliedAlpha:y.premultipliedAlpha,doubleSided:y.side===sn,flipSided:y.side===Ie,useDepthPacking:y.depthPacking>=0,depthPacking:y.depthPacking||0,index0AttributeName:y.index0AttributeName,extensionClipCullDistance:mt&&y.extensions.clipCullDistance===!0&&n.has("WEBGL_clip_cull_distance"),extensionMultiDraw:(mt&&y.extensions.multiDraw===!0||St)&&n.has("WEBGL_multi_draw"),rendererExtensionParallelShaderCompile:n.has("KHR_parallel_shader_compile"),customProgramCacheKey:y.customProgramCacheKey()};return ee.vertexUv1s=c.has(1),ee.vertexUv2s=c.has(2),ee.vertexUv3s=c.has(3),c.clear(),ee}function f(y){const x=[];if(y.shaderID?x.push(y.shaderID):(x.push(y.customVertexShaderID),x.push(y.customFragmentShaderID)),y.defines!==void 0)for(const L in y.defines)x.push(L),x.push(y.defines[L]);return y.isRawShaderMaterial===!1&&(E(x,y),b(x,y),x.push(s.outputColorSpace)),x.push(y.customProgramCacheKey),x.join()}function E(y,x){y.push(x.precision),y.push(x.outputColorSpace),y.push(x.envMapMode),y.push(x.envMapCubeUVHeight),y.push(x.mapUv),y.push(x.alphaMapUv),y.push(x.lightMapUv),y.push(x.aoMapUv),y.push(x.bumpMapUv),y.push(x.normalMapUv),y.push(x.displacementMapUv),y.push(x.emissiveMapUv),y.push(x.metalnessMapUv),y.push(x.roughnessMapUv),y.push(x.anisotropyMapUv),y.push(x.clearcoatMapUv),y.push(x.clearcoatNormalMapUv),y.push(x.clearcoatRoughnessMapUv),y.push(x.iridescenceMapUv),y.push(x.iridescenceThicknessMapUv),y.push(x.sheenColorMapUv),y.push(x.sheenRoughnessMapUv),y.push(x.specularMapUv),y.push(x.specularColorMapUv),y.push(x.specularIntensityMapUv),y.push(x.transmissionMapUv),y.push(x.thicknessMapUv),y.push(x.combine),y.push(x.fogExp2),y.push(x.sizeAttenuation),y.push(x.morphTargetsCount),y.push(x.morphAttributeCount),y.push(x.numDirLights),y.push(x.numPointLights),y.push(x.numSpotLights),y.push(x.numSpotLightMaps),y.push(x.numHemiLights),y.push(x.numRectAreaLights),y.push(x.numDirLightShadows),y.push(x.numPointLightShadows),y.push(x.numSpotLightShadows),y.push(x.numSpotLightShadowsWithMaps),y.push(x.numLightProbes),y.push(x.shadowMapType),y.push(x.toneMapping),y.push(x.numClippingPlanes),y.push(x.numClipIntersection),y.push(x.depthPacking)}function b(y,x){o.disableAll(),x.supportsVertexTextures&&o.enable(0),x.instancing&&o.enable(1),x.instancingColor&&o.enable(2),x.instancingMorph&&o.enable(3),x.matcap&&o.enable(4),x.envMap&&o.enable(5),x.normalMapObjectSpace&&o.enable(6),x.normalMapTangentSpace&&o.enable(7),x.clearcoat&&o.enable(8),x.iridescence&&o.enable(9),x.alphaTest&&o.enable(10),x.vertexColors&&o.enable(11),x.vertexAlphas&&o.enable(12),x.vertexUv1s&&o.enable(13),x.vertexUv2s&&o.enable(14),x.vertexUv3s&&o.enable(15),x.vertexTangents&&o.enable(16),x.anisotropy&&o.enable(17),x.alphaHash&&o.enable(18),x.batching&&o.enable(19),x.dispersion&&o.enable(20),x.batchingColor&&o.enable(21),x.gradientMap&&o.enable(22),y.push(o.mask),o.disableAll(),x.fog&&o.enable(0),x.useFog&&o.enable(1),x.flatShading&&o.enable(2),x.logarithmicDepthBuffer&&o.enable(3),x.reversedDepthBuffer&&o.enable(4),x.skinning&&o.enable(5),x.morphTargets&&o.enable(6),x.morphNormals&&o.enable(7),x.morphColors&&o.enable(8),x.premultipliedAlpha&&o.enable(9),x.shadowMapEnabled&&o.enable(10),x.doubleSided&&o.enable(11),x.flipSided&&o.enable(12),x.useDepthPacking&&o.enable(13),x.dithering&&o.enable(14),x.transmission&&o.enable(15),x.sheen&&o.enable(16),x.opaque&&o.enable(17),x.pointsUvs&&o.enable(18),x.decodeVideoTexture&&o.enable(19),x.decodeVideoTextureEmissive&&o.enable(20),x.alphaToCoverage&&o.enable(21),y.push(o.mask)}function S(y){const x=g[y.type];let L;if(x){const N=en[x];L=Zi.clone(N.uniforms)}else L=y.uniforms;return L}function w(y,x){let L;for(let N=0,k=h.length;N<k;N++){const X=h[N];if(X.cacheKey===x){L=X,++L.usedTimes;break}}return L===void 0&&(L=new rm(s,x,y,r),h.push(L)),L}function A(y){if(--y.usedTimes===0){const x=h.indexOf(y);h[x]=h[h.length-1],h.pop(),y.destroy()}}function C(y){l.remove(y)}function D(){l.dispose()}return{getParameters:m,getProgramCacheKey:f,getUniforms:S,acquireProgram:w,releaseProgram:A,releaseShaderCache:C,programs:h,dispose:D}}function hm(){let s=new WeakMap;function t(a){return s.has(a)}function e(a){let o=s.get(a);return o===void 0&&(o={},s.set(a,o)),o}function n(a){s.delete(a)}function i(a,o,l){s.get(a)[o]=l}function r(){s=new WeakMap}return{has:t,get:e,remove:n,update:i,dispose:r}}function um(s,t){return s.groupOrder!==t.groupOrder?s.groupOrder-t.groupOrder:s.renderOrder!==t.renderOrder?s.renderOrder-t.renderOrder:s.material.id!==t.material.id?s.material.id-t.material.id:s.z!==t.z?s.z-t.z:s.id-t.id}function sl(s,t){return s.groupOrder!==t.groupOrder?s.groupOrder-t.groupOrder:s.renderOrder!==t.renderOrder?s.renderOrder-t.renderOrder:s.z!==t.z?t.z-s.z:s.id-t.id}function rl(){const s=[];let t=0;const e=[],n=[],i=[];function r(){t=0,e.length=0,n.length=0,i.length=0}function a(d,u,p,g,v,m){let f=s[t];return f===void 0?(f={id:d.id,object:d,geometry:u,material:p,groupOrder:g,renderOrder:d.renderOrder,z:v,group:m},s[t]=f):(f.id=d.id,f.object=d,f.geometry=u,f.material=p,f.groupOrder=g,f.renderOrder=d.renderOrder,f.z=v,f.group=m),t++,f}function o(d,u,p,g,v,m){const f=a(d,u,p,g,v,m);p.transmission>0?n.push(f):p.transparent===!0?i.push(f):e.push(f)}function l(d,u,p,g,v,m){const f=a(d,u,p,g,v,m);p.transmission>0?n.unshift(f):p.transparent===!0?i.unshift(f):e.unshift(f)}function c(d,u){e.length>1&&e.sort(d||um),n.length>1&&n.sort(u||sl),i.length>1&&i.sort(u||sl)}function h(){for(let d=t,u=s.length;d<u;d++){const p=s[d];if(p.id===null)break;p.id=null,p.object=null,p.geometry=null,p.material=null,p.group=null}}return{opaque:e,transmissive:n,transparent:i,init:r,push:o,unshift:l,finish:h,sort:c}}function dm(){let s=new WeakMap;function t(n,i){const r=s.get(n);let a;return r===void 0?(a=new rl,s.set(n,[a])):i>=r.length?(a=new rl,r.push(a)):a=r[i],a}function e(){s=new WeakMap}return{get:t,dispose:e}}function fm(){const s={};return{get:function(t){if(s[t.id]!==void 0)return s[t.id];let e;switch(t.type){case"DirectionalLight":e={direction:new P,color:new Ft};break;case"SpotLight":e={position:new P,direction:new P,color:new Ft,distance:0,coneCos:0,penumbraCos:0,decay:0};break;case"PointLight":e={position:new P,color:new Ft,distance:0,decay:0};break;case"HemisphereLight":e={direction:new P,skyColor:new Ft,groundColor:new Ft};break;case"RectAreaLight":e={color:new Ft,position:new P,halfWidth:new P,halfHeight:new P};break}return s[t.id]=e,e}}}function pm(){const s={};return{get:function(t){if(s[t.id]!==void 0)return s[t.id];let e;switch(t.type){case"DirectionalLight":e={shadowIntensity:1,shadowBias:0,shadowNormalBias:0,shadowRadius:1,shadowMapSize:new dt};break;case"SpotLight":e={shadowIntensity:1,shadowBias:0,shadowNormalBias:0,shadowRadius:1,shadowMapSize:new dt};break;case"PointLight":e={shadowIntensity:1,shadowBias:0,shadowNormalBias:0,shadowRadius:1,shadowMapSize:new dt,shadowCameraNear:1,shadowCameraFar:1e3};break}return s[t.id]=e,e}}}let mm=0;function gm(s,t){return(t.castShadow?2:0)-(s.castShadow?2:0)+(t.map?1:0)-(s.map?1:0)}function _m(s){const t=new fm,e=pm(),n={version:0,hash:{directionalLength:-1,pointLength:-1,spotLength:-1,rectAreaLength:-1,hemiLength:-1,numDirectionalShadows:-1,numPointShadows:-1,numSpotShadows:-1,numSpotMaps:-1,numLightProbes:-1},ambient:[0,0,0],probe:[],directional:[],directionalShadow:[],directionalShadowMap:[],directionalShadowMatrix:[],spot:[],spotLightMap:[],spotShadow:[],spotShadowMap:[],spotLightMatrix:[],rectArea:[],rectAreaLTC1:null,rectAreaLTC2:null,point:[],pointShadow:[],pointShadowMap:[],pointShadowMatrix:[],hemi:[],numSpotLightShadowsWithMaps:0,numLightProbes:0};for(let c=0;c<9;c++)n.probe.push(new P);const i=new P,r=new Kt,a=new Kt;function o(c){let h=0,d=0,u=0;for(let y=0;y<9;y++)n.probe[y].set(0,0,0);let p=0,g=0,v=0,m=0,f=0,E=0,b=0,S=0,w=0,A=0,C=0;c.sort(gm);for(let y=0,x=c.length;y<x;y++){const L=c[y],N=L.color,k=L.intensity,X=L.distance,W=L.shadow&&L.shadow.map?L.shadow.map.texture:null;if(L.isAmbientLight)h+=N.r*k,d+=N.g*k,u+=N.b*k;else if(L.isLightProbe){for(let q=0;q<9;q++)n.probe[q].addScaledVector(L.sh.coefficients[q],k);C++}else if(L.isDirectionalLight){const q=t.get(L);if(q.color.copy(L.color).multiplyScalar(L.intensity),L.castShadow){const Z=L.shadow,H=e.get(L);H.shadowIntensity=Z.intensity,H.shadowBias=Z.bias,H.shadowNormalBias=Z.normalBias,H.shadowRadius=Z.radius,H.shadowMapSize=Z.mapSize,n.directionalShadow[p]=H,n.directionalShadowMap[p]=W,n.directionalShadowMatrix[p]=L.shadow.matrix,E++}n.directional[p]=q,p++}else if(L.isSpotLight){const q=t.get(L);q.position.setFromMatrixPosition(L.matrixWorld),q.color.copy(N).multiplyScalar(k),q.distance=X,q.coneCos=Math.cos(L.angle),q.penumbraCos=Math.cos(L.angle*(1-L.penumbra)),q.decay=L.decay,n.spot[v]=q;const Z=L.shadow;if(L.map&&(n.spotLightMap[w]=L.map,w++,Z.updateMatrices(L),L.castShadow&&A++),n.spotLightMatrix[v]=Z.matrix,L.castShadow){const H=e.get(L);H.shadowIntensity=Z.intensity,H.shadowBias=Z.bias,H.shadowNormalBias=Z.normalBias,H.shadowRadius=Z.radius,H.shadowMapSize=Z.mapSize,n.spotShadow[v]=H,n.spotShadowMap[v]=W,S++}v++}else if(L.isRectAreaLight){const q=t.get(L);q.color.copy(N).multiplyScalar(k),q.halfWidth.set(L.width*.5,0,0),q.halfHeight.set(0,L.height*.5,0),n.rectArea[m]=q,m++}else if(L.isPointLight){const q=t.get(L);if(q.color.copy(L.color).multiplyScalar(L.intensity),q.distance=L.distance,q.decay=L.decay,L.castShadow){const Z=L.shadow,H=e.get(L);H.shadowIntensity=Z.intensity,H.shadowBias=Z.bias,H.shadowNormalBias=Z.normalBias,H.shadowRadius=Z.radius,H.shadowMapSize=Z.mapSize,H.shadowCameraNear=Z.camera.near,H.shadowCameraFar=Z.camera.far,n.pointShadow[g]=H,n.pointShadowMap[g]=W,n.pointShadowMatrix[g]=L.shadow.matrix,b++}n.point[g]=q,g++}else if(L.isHemisphereLight){const q=t.get(L);q.skyColor.copy(L.color).multiplyScalar(k),q.groundColor.copy(L.groundColor).multiplyScalar(k),n.hemi[f]=q,f++}}m>0&&(s.has("OES_texture_float_linear")===!0?(n.rectAreaLTC1=st.LTC_FLOAT_1,n.rectAreaLTC2=st.LTC_FLOAT_2):(n.rectAreaLTC1=st.LTC_HALF_1,n.rectAreaLTC2=st.LTC_HALF_2)),n.ambient[0]=h,n.ambient[1]=d,n.ambient[2]=u;const D=n.hash;(D.directionalLength!==p||D.pointLength!==g||D.spotLength!==v||D.rectAreaLength!==m||D.hemiLength!==f||D.numDirectionalShadows!==E||D.numPointShadows!==b||D.numSpotShadows!==S||D.numSpotMaps!==w||D.numLightProbes!==C)&&(n.directional.length=p,n.spot.length=v,n.rectArea.length=m,n.point.length=g,n.hemi.length=f,n.directionalShadow.length=E,n.directionalShadowMap.length=E,n.pointShadow.length=b,n.pointShadowMap.length=b,n.spotShadow.length=S,n.spotShadowMap.length=S,n.directionalShadowMatrix.length=E,n.pointShadowMatrix.length=b,n.spotLightMatrix.length=S+w-A,n.spotLightMap.length=w,n.numSpotLightShadowsWithMaps=A,n.numLightProbes=C,D.directionalLength=p,D.pointLength=g,D.spotLength=v,D.rectAreaLength=m,D.hemiLength=f,D.numDirectionalShadows=E,D.numPointShadows=b,D.numSpotShadows=S,D.numSpotMaps=w,D.numLightProbes=C,n.version=mm++)}function l(c,h){let d=0,u=0,p=0,g=0,v=0;const m=h.matrixWorldInverse;for(let f=0,E=c.length;f<E;f++){const b=c[f];if(b.isDirectionalLight){const S=n.directional[d];S.direction.setFromMatrixPosition(b.matrixWorld),i.setFromMatrixPosition(b.target.matrixWorld),S.direction.sub(i),S.direction.transformDirection(m),d++}else if(b.isSpotLight){const S=n.spot[p];S.position.setFromMatrixPosition(b.matrixWorld),S.position.applyMatrix4(m),S.direction.setFromMatrixPosition(b.matrixWorld),i.setFromMatrixPosition(b.target.matrixWorld),S.direction.sub(i),S.direction.transformDirection(m),p++}else if(b.isRectAreaLight){const S=n.rectArea[g];S.position.setFromMatrixPosition(b.matrixWorld),S.position.applyMatrix4(m),a.identity(),r.copy(b.matrixWorld),r.premultiply(m),a.extractRotation(r),S.halfWidth.set(b.width*.5,0,0),S.halfHeight.set(0,b.height*.5,0),S.halfWidth.applyMatrix4(a),S.halfHeight.applyMatrix4(a),g++}else if(b.isPointLight){const S=n.point[u];S.position.setFromMatrixPosition(b.matrixWorld),S.position.applyMatrix4(m),u++}else if(b.isHemisphereLight){const S=n.hemi[v];S.direction.setFromMatrixPosition(b.matrixWorld),S.direction.transformDirection(m),v++}}}return{setup:o,setupView:l,state:n}}function al(s){const t=new _m(s),e=[],n=[];function i(h){c.camera=h,e.length=0,n.length=0}function r(h){e.push(h)}function a(h){n.push(h)}function o(){t.setup(e)}function l(h){t.setupView(e,h)}const c={lightsArray:e,shadowsArray:n,camera:null,lights:t,transmissionRenderTarget:{}};return{init:i,state:c,setupLights:o,setupLightsView:l,pushLight:r,pushShadow:a}}function xm(s){let t=new WeakMap;function e(i,r=0){const a=t.get(i);let o;return a===void 0?(o=new al(s),t.set(i,[o])):r>=a.length?(o=new al(s),a.push(o)):o=a[r],o}function n(){t=new WeakMap}return{get:e,dispose:n}}const vm=`void main() {
	gl_Position = vec4( position, 1.0 );
}`,ym=`uniform sampler2D shadow_pass;
uniform vec2 resolution;
uniform float radius;
#include <packing>
void main() {
	const float samples = float( VSM_SAMPLES );
	float mean = 0.0;
	float squared_mean = 0.0;
	float uvStride = samples <= 1.0 ? 0.0 : 2.0 / ( samples - 1.0 );
	float uvStart = samples <= 1.0 ? 0.0 : - 1.0;
	for ( float i = 0.0; i < samples; i ++ ) {
		float uvOffset = uvStart + i * uvStride;
		#ifdef HORIZONTAL_PASS
			vec2 distribution = unpackRGBATo2Half( texture2D( shadow_pass, ( gl_FragCoord.xy + vec2( uvOffset, 0.0 ) * radius ) / resolution ) );
			mean += distribution.x;
			squared_mean += distribution.y * distribution.y + distribution.x * distribution.x;
		#else
			float depth = unpackRGBAToDepth( texture2D( shadow_pass, ( gl_FragCoord.xy + vec2( 0.0, uvOffset ) * radius ) / resolution ) );
			mean += depth;
			squared_mean += depth * depth;
		#endif
	}
	mean = mean / samples;
	squared_mean = squared_mean / samples;
	float std_dev = sqrt( squared_mean - mean * mean );
	gl_FragColor = pack2HalfToRGBA( vec2( mean, std_dev ) );
}`;function Mm(s,t,e){let n=new Ua;const i=new dt,r=new dt,a=new Jt,o=new Hh({depthPacking:Pc}),l=new Vh,c={},h=e.maxTextureSize,d={[Dn]:Ie,[Ie]:Dn,[sn]:sn},u=new Ce({defines:{VSM_SAMPLES:8},uniforms:{shadow_pass:{value:null},resolution:{value:new dt},radius:{value:4}},vertexShader:vm,fragmentShader:ym}),p=u.clone();p.defines.HORIZONTAL_PASS=1;const g=new me;g.setAttribute("position",new Xe(new Float32Array([-1,-1,.5,3,-1,.5,-1,3,.5]),3));const v=new Me(g,u),m=this;this.enabled=!1,this.autoUpdate=!0,this.needsUpdate=!1,this.type=pl;let f=this.type;this.render=function(A,C,D){if(m.enabled===!1||m.autoUpdate===!1&&m.needsUpdate===!1||A.length===0)return;const y=s.getRenderTarget(),x=s.getActiveCubeFace(),L=s.getActiveMipmapLevel(),N=s.state;N.setBlending(vn),N.buffers.depth.getReversed()===!0?N.buffers.color.setClear(0,0,0,0):N.buffers.color.setClear(1,1,1,1),N.buffers.depth.setTest(!0),N.setScissorTest(!1);const k=f!==_n&&this.type===_n,X=f===_n&&this.type!==_n;for(let W=0,q=A.length;W<q;W++){const Z=A[W],H=Z.shadow;if(H===void 0){console.warn("THREE.WebGLShadowMap:",Z,"has no shadow.");continue}if(H.autoUpdate===!1&&H.needsUpdate===!1)continue;i.copy(H.mapSize);const rt=H.getFrameExtents();if(i.multiply(rt),r.copy(H.mapSize),(i.x>h||i.y>h)&&(i.x>h&&(r.x=Math.floor(h/rt.x),i.x=r.x*rt.x,H.mapSize.x=r.x),i.y>h&&(r.y=Math.floor(h/rt.y),i.y=r.y*rt.y,H.mapSize.y=r.y)),H.map===null||k===!0||X===!0){const Et=this.type!==_n?{minFilter:ze,magFilter:ze}:{};H.map!==null&&H.map.dispose(),H.map=new $e(i.x,i.y,Et),H.map.texture.name=Z.name+".shadowMap",H.camera.updateProjectionMatrix()}s.setRenderTarget(H.map),s.clear();const ct=H.getViewportCount();for(let Et=0;Et<ct;Et++){const kt=H.getViewport(Et);a.set(r.x*kt.x,r.y*kt.y,r.x*kt.z,r.y*kt.w),N.viewport(a),H.updateMatrices(Z,Et),n=H.getFrustum(),S(C,D,H.camera,Z,this.type)}H.isPointLightShadow!==!0&&this.type===_n&&E(H,D),H.needsUpdate=!1}f=this.type,m.needsUpdate=!1,s.setRenderTarget(y,x,L)};function E(A,C){const D=t.update(v);u.defines.VSM_SAMPLES!==A.blurSamples&&(u.defines.VSM_SAMPLES=A.blurSamples,p.defines.VSM_SAMPLES=A.blurSamples,u.needsUpdate=!0,p.needsUpdate=!0),A.mapPass===null&&(A.mapPass=new $e(i.x,i.y)),u.uniforms.shadow_pass.value=A.map.texture,u.uniforms.resolution.value=A.mapSize,u.uniforms.radius.value=A.radius,s.setRenderTarget(A.mapPass),s.clear(),s.renderBufferDirect(C,null,D,u,v,null),p.uniforms.shadow_pass.value=A.mapPass.texture,p.uniforms.resolution.value=A.mapSize,p.uniforms.radius.value=A.radius,s.setRenderTarget(A.map),s.clear(),s.renderBufferDirect(C,null,D,p,v,null)}function b(A,C,D,y){let x=null;const L=D.isPointLight===!0?A.customDistanceMaterial:A.customDepthMaterial;if(L!==void 0)x=L;else if(x=D.isPointLight===!0?l:o,s.localClippingEnabled&&C.clipShadows===!0&&Array.isArray(C.clippingPlanes)&&C.clippingPlanes.length!==0||C.displacementMap&&C.displacementScale!==0||C.alphaMap&&C.alphaTest>0||C.map&&C.alphaTest>0||C.alphaToCoverage===!0){const N=x.uuid,k=C.uuid;let X=c[N];X===void 0&&(X={},c[N]=X);let W=X[k];W===void 0&&(W=x.clone(),X[k]=W,C.addEventListener("dispose",w)),x=W}if(x.visible=C.visible,x.wireframe=C.wireframe,y===_n?x.side=C.shadowSide!==null?C.shadowSide:C.side:x.side=C.shadowSide!==null?C.shadowSide:d[C.side],x.alphaMap=C.alphaMap,x.alphaTest=C.alphaToCoverage===!0?.5:C.alphaTest,x.map=C.map,x.clipShadows=C.clipShadows,x.clippingPlanes=C.clippingPlanes,x.clipIntersection=C.clipIntersection,x.displacementMap=C.displacementMap,x.displacementScale=C.displacementScale,x.displacementBias=C.displacementBias,x.wireframeLinewidth=C.wireframeLinewidth,x.linewidth=C.linewidth,D.isPointLight===!0&&x.isMeshDistanceMaterial===!0){const N=s.properties.get(x);N.light=D}return x}function S(A,C,D,y,x){if(A.visible===!1)return;if(A.layers.test(C.layers)&&(A.isMesh||A.isLine||A.isPoints)&&(A.castShadow||A.receiveShadow&&x===_n)&&(!A.frustumCulled||n.intersectsObject(A))){A.modelViewMatrix.multiplyMatrices(D.matrixWorldInverse,A.matrixWorld);const k=t.update(A),X=A.material;if(Array.isArray(X)){const W=k.groups;for(let q=0,Z=W.length;q<Z;q++){const H=W[q],rt=X[H.materialIndex];if(rt&&rt.visible){const ct=b(A,rt,y,x);A.onBeforeShadow(s,A,C,D,k,ct,H),s.renderBufferDirect(D,null,k,ct,A,H),A.onAfterShadow(s,A,C,D,k,ct,H)}}}else if(X.visible){const W=b(A,X,y,x);A.onBeforeShadow(s,A,C,D,k,W,null),s.renderBufferDirect(D,null,k,W,A,null),A.onAfterShadow(s,A,C,D,k,W,null)}}const N=A.children;for(let k=0,X=N.length;k<X;k++)S(N[k],C,D,y,x)}function w(A){A.target.removeEventListener("dispose",w);for(const D in c){const y=c[D],x=A.target.uuid;x in y&&(y[x].dispose(),delete y[x])}}}const Sm={[Ir]:Ur,[Nr]:Br,[Fr]:zr,[yi]:Or,[Ur]:Ir,[Br]:Nr,[zr]:Fr,[Or]:yi};function Em(s,t){function e(){let I=!1;const et=new Jt;let it=null;const ut=new Jt(0,0,0,0);return{setMask:function(J){it!==J&&!I&&(s.colorMask(J,J,J,J),it=J)},setLocked:function(J){I=J},setClear:function(J,j,mt,Lt,ee){ee===!0&&(J*=Lt,j*=Lt,mt*=Lt),et.set(J,j,mt,Lt),ut.equals(et)===!1&&(s.clearColor(J,j,mt,Lt),ut.copy(et))},reset:function(){I=!1,it=null,ut.set(-1,0,0,0)}}}function n(){let I=!1,et=!1,it=null,ut=null,J=null;return{setReversed:function(j){if(et!==j){const mt=t.get("EXT_clip_control");j?mt.clipControlEXT(mt.LOWER_LEFT_EXT,mt.ZERO_TO_ONE_EXT):mt.clipControlEXT(mt.LOWER_LEFT_EXT,mt.NEGATIVE_ONE_TO_ONE_EXT),et=j;const Lt=J;J=null,this.setClear(Lt)}},getReversed:function(){return et},setTest:function(j){j?$(s.DEPTH_TEST):ft(s.DEPTH_TEST)},setMask:function(j){it!==j&&!I&&(s.depthMask(j),it=j)},setFunc:function(j){if(et&&(j=Sm[j]),ut!==j){switch(j){case Ir:s.depthFunc(s.NEVER);break;case Ur:s.depthFunc(s.ALWAYS);break;case Nr:s.depthFunc(s.LESS);break;case yi:s.depthFunc(s.LEQUAL);break;case Fr:s.depthFunc(s.EQUAL);break;case Or:s.depthFunc(s.GEQUAL);break;case Br:s.depthFunc(s.GREATER);break;case zr:s.depthFunc(s.NOTEQUAL);break;default:s.depthFunc(s.LEQUAL)}ut=j}},setLocked:function(j){I=j},setClear:function(j){J!==j&&(et&&(j=1-j),s.clearDepth(j),J=j)},reset:function(){I=!1,it=null,ut=null,J=null,et=!1}}}function i(){let I=!1,et=null,it=null,ut=null,J=null,j=null,mt=null,Lt=null,ee=null;return{setTest:function(Yt){I||(Yt?$(s.STENCIL_TEST):ft(s.STENCIL_TEST))},setMask:function(Yt){et!==Yt&&!I&&(s.stencilMask(Yt),et=Yt)},setFunc:function(Yt,hn,Je){(it!==Yt||ut!==hn||J!==Je)&&(s.stencilFunc(Yt,hn,Je),it=Yt,ut=hn,J=Je)},setOp:function(Yt,hn,Je){(j!==Yt||mt!==hn||Lt!==Je)&&(s.stencilOp(Yt,hn,Je),j=Yt,mt=hn,Lt=Je)},setLocked:function(Yt){I=Yt},setClear:function(Yt){ee!==Yt&&(s.clearStencil(Yt),ee=Yt)},reset:function(){I=!1,et=null,it=null,ut=null,J=null,j=null,mt=null,Lt=null,ee=null}}}const r=new e,a=new n,o=new i,l=new WeakMap,c=new WeakMap;let h={},d={},u=new WeakMap,p=[],g=null,v=!1,m=null,f=null,E=null,b=null,S=null,w=null,A=null,C=new Ft(0,0,0),D=0,y=!1,x=null,L=null,N=null,k=null,X=null;const W=s.getParameter(s.MAX_COMBINED_TEXTURE_IMAGE_UNITS);let q=!1,Z=0;const H=s.getParameter(s.VERSION);H.indexOf("WebGL")!==-1?(Z=parseFloat(/^WebGL (\d)/.exec(H)[1]),q=Z>=1):H.indexOf("OpenGL ES")!==-1&&(Z=parseFloat(/^OpenGL ES (\d)/.exec(H)[1]),q=Z>=2);let rt=null,ct={};const Et=s.getParameter(s.SCISSOR_BOX),kt=s.getParameter(s.VIEWPORT),te=new Jt().fromArray(Et),se=new Jt().fromArray(kt);function qt(I,et,it,ut){const J=new Uint8Array(4),j=s.createTexture();s.bindTexture(I,j),s.texParameteri(I,s.TEXTURE_MIN_FILTER,s.NEAREST),s.texParameteri(I,s.TEXTURE_MAG_FILTER,s.NEAREST);for(let mt=0;mt<it;mt++)I===s.TEXTURE_3D||I===s.TEXTURE_2D_ARRAY?s.texImage3D(et,0,s.RGBA,1,1,ut,0,s.RGBA,s.UNSIGNED_BYTE,J):s.texImage2D(et+mt,0,s.RGBA,1,1,0,s.RGBA,s.UNSIGNED_BYTE,J);return j}const Y={};Y[s.TEXTURE_2D]=qt(s.TEXTURE_2D,s.TEXTURE_2D,1),Y[s.TEXTURE_CUBE_MAP]=qt(s.TEXTURE_CUBE_MAP,s.TEXTURE_CUBE_MAP_POSITIVE_X,6),Y[s.TEXTURE_2D_ARRAY]=qt(s.TEXTURE_2D_ARRAY,s.TEXTURE_2D_ARRAY,1,1),Y[s.TEXTURE_3D]=qt(s.TEXTURE_3D,s.TEXTURE_3D,1,1),r.setClear(0,0,0,1),a.setClear(1),o.setClear(0),$(s.DEPTH_TEST),a.setFunc(yi),Ct(!1),gt(Za),$(s.CULL_FACE),re(vn);function $(I){h[I]!==!0&&(s.enable(I),h[I]=!0)}function ft(I){h[I]!==!1&&(s.disable(I),h[I]=!1)}function Pt(I,et){return d[I]!==et?(s.bindFramebuffer(I,et),d[I]=et,I===s.DRAW_FRAMEBUFFER&&(d[s.FRAMEBUFFER]=et),I===s.FRAMEBUFFER&&(d[s.DRAW_FRAMEBUFFER]=et),!0):!1}function St(I,et){let it=p,ut=!1;if(I){it=u.get(et),it===void 0&&(it=[],u.set(et,it));const J=I.textures;if(it.length!==J.length||it[0]!==s.COLOR_ATTACHMENT0){for(let j=0,mt=J.length;j<mt;j++)it[j]=s.COLOR_ATTACHMENT0+j;it.length=J.length,ut=!0}}else it[0]!==s.BACK&&(it[0]=s.BACK,ut=!0);ut&&s.drawBuffers(it)}function Vt(I){return g!==I?(s.useProgram(I),g=I,!0):!1}const Se={[Vn]:s.FUNC_ADD,[cc]:s.FUNC_SUBTRACT,[hc]:s.FUNC_REVERSE_SUBTRACT};Se[uc]=s.MIN,Se[dc]=s.MAX;const R={[fc]:s.ZERO,[pc]:s.ONE,[mc]:s.SRC_COLOR,[Lr]:s.SRC_ALPHA,[Mc]:s.SRC_ALPHA_SATURATE,[vc]:s.DST_COLOR,[_c]:s.DST_ALPHA,[gc]:s.ONE_MINUS_SRC_COLOR,[Dr]:s.ONE_MINUS_SRC_ALPHA,[yc]:s.ONE_MINUS_DST_COLOR,[xc]:s.ONE_MINUS_DST_ALPHA,[Sc]:s.CONSTANT_COLOR,[Ec]:s.ONE_MINUS_CONSTANT_COLOR,[bc]:s.CONSTANT_ALPHA,[Tc]:s.ONE_MINUS_CONSTANT_ALPHA};function re(I,et,it,ut,J,j,mt,Lt,ee,Yt){if(I===vn){v===!0&&(ft(s.BLEND),v=!1);return}if(v===!1&&($(s.BLEND),v=!0),I!==lc){if(I!==m||Yt!==y){if((f!==Vn||S!==Vn)&&(s.blendEquation(s.FUNC_ADD),f=Vn,S=Vn),Yt)switch(I){case xi:s.blendFuncSeparate(s.ONE,s.ONE_MINUS_SRC_ALPHA,s.ONE,s.ONE_MINUS_SRC_ALPHA);break;case Pr:s.blendFunc(s.ONE,s.ONE);break;case $a:s.blendFuncSeparate(s.ZERO,s.ONE_MINUS_SRC_COLOR,s.ZERO,s.ONE);break;case Ja:s.blendFuncSeparate(s.DST_COLOR,s.ONE_MINUS_SRC_ALPHA,s.ZERO,s.ONE);break;default:console.error("THREE.WebGLState: Invalid blending: ",I);break}else switch(I){case xi:s.blendFuncSeparate(s.SRC_ALPHA,s.ONE_MINUS_SRC_ALPHA,s.ONE,s.ONE_MINUS_SRC_ALPHA);break;case Pr:s.blendFuncSeparate(s.SRC_ALPHA,s.ONE,s.ONE,s.ONE);break;case $a:console.error("THREE.WebGLState: SubtractiveBlending requires material.premultipliedAlpha = true");break;case Ja:console.error("THREE.WebGLState: MultiplyBlending requires material.premultipliedAlpha = true");break;default:console.error("THREE.WebGLState: Invalid blending: ",I);break}E=null,b=null,w=null,A=null,C.set(0,0,0),D=0,m=I,y=Yt}return}J=J||et,j=j||it,mt=mt||ut,(et!==f||J!==S)&&(s.blendEquationSeparate(Se[et],Se[J]),f=et,S=J),(it!==E||ut!==b||j!==w||mt!==A)&&(s.blendFuncSeparate(R[it],R[ut],R[j],R[mt]),E=it,b=ut,w=j,A=mt),(Lt.equals(C)===!1||ee!==D)&&(s.blendColor(Lt.r,Lt.g,Lt.b,ee),C.copy(Lt),D=ee),m=I,y=!1}function It(I,et){I.side===sn?ft(s.CULL_FACE):$(s.CULL_FACE);let it=I.side===Ie;et&&(it=!it),Ct(it),I.blending===xi&&I.transparent===!1?re(vn):re(I.blending,I.blendEquation,I.blendSrc,I.blendDst,I.blendEquationAlpha,I.blendSrcAlpha,I.blendDstAlpha,I.blendColor,I.blendAlpha,I.premultipliedAlpha),a.setFunc(I.depthFunc),a.setTest(I.depthTest),a.setMask(I.depthWrite),r.setMask(I.colorWrite);const ut=I.stencilWrite;o.setTest(ut),ut&&(o.setMask(I.stencilWriteMask),o.setFunc(I.stencilFunc,I.stencilRef,I.stencilFuncMask),o.setOp(I.stencilFail,I.stencilZFail,I.stencilZPass)),_t(I.polygonOffset,I.polygonOffsetFactor,I.polygonOffsetUnits),I.alphaToCoverage===!0?$(s.SAMPLE_ALPHA_TO_COVERAGE):ft(s.SAMPLE_ALPHA_TO_COVERAGE)}function Ct(I){x!==I&&(I?s.frontFace(s.CW):s.frontFace(s.CCW),x=I)}function gt(I){I!==ac?($(s.CULL_FACE),I!==L&&(I===Za?s.cullFace(s.BACK):I===oc?s.cullFace(s.FRONT):s.cullFace(s.FRONT_AND_BACK))):ft(s.CULL_FACE),L=I}function ae(I){I!==N&&(q&&s.lineWidth(I),N=I)}function _t(I,et,it){I?($(s.POLYGON_OFFSET_FILL),(k!==et||X!==it)&&(s.polygonOffset(et,it),k=et,X=it)):ft(s.POLYGON_OFFSET_FILL)}function Ot(I){I?$(s.SCISSOR_TEST):ft(s.SCISSOR_TEST)}function xe(I){I===void 0&&(I=s.TEXTURE0+W-1),rt!==I&&(s.activeTexture(I),rt=I)}function he(I,et,it){it===void 0&&(rt===null?it=s.TEXTURE0+W-1:it=rt);let ut=ct[it];ut===void 0&&(ut={type:void 0,texture:void 0},ct[it]=ut),(ut.type!==I||ut.texture!==et)&&(rt!==it&&(s.activeTexture(it),rt=it),s.bindTexture(I,et||Y[I]),ut.type=I,ut.texture=et)}function T(){const I=ct[rt];I!==void 0&&I.type!==void 0&&(s.bindTexture(I.type,null),I.type=void 0,I.texture=void 0)}function _(){try{s.compressedTexImage2D(...arguments)}catch(I){console.error("THREE.WebGLState:",I)}}function O(){try{s.compressedTexImage3D(...arguments)}catch(I){console.error("THREE.WebGLState:",I)}}function G(){try{s.texSubImage2D(...arguments)}catch(I){console.error("THREE.WebGLState:",I)}}function K(){try{s.texSubImage3D(...arguments)}catch(I){console.error("THREE.WebGLState:",I)}}function V(){try{s.compressedTexSubImage2D(...arguments)}catch(I){console.error("THREE.WebGLState:",I)}}function Mt(){try{s.compressedTexSubImage3D(...arguments)}catch(I){console.error("THREE.WebGLState:",I)}}function nt(){try{s.texStorage2D(...arguments)}catch(I){console.error("THREE.WebGLState:",I)}}function xt(){try{s.texStorage3D(...arguments)}catch(I){console.error("THREE.WebGLState:",I)}}function vt(){try{s.texImage2D(...arguments)}catch(I){console.error("THREE.WebGLState:",I)}}function tt(){try{s.texImage3D(...arguments)}catch(I){console.error("THREE.WebGLState:",I)}}function lt(I){te.equals(I)===!1&&(s.scissor(I.x,I.y,I.z,I.w),te.copy(I))}function At(I){se.equals(I)===!1&&(s.viewport(I.x,I.y,I.z,I.w),se.copy(I))}function yt(I,et){let it=c.get(et);it===void 0&&(it=new WeakMap,c.set(et,it));let ut=it.get(I);ut===void 0&&(ut=s.getUniformBlockIndex(et,I.name),it.set(I,ut))}function at(I,et){const ut=c.get(et).get(I);l.get(et)!==ut&&(s.uniformBlockBinding(et,ut,I.__bindingPointIndex),l.set(et,ut))}function Ut(){s.disable(s.BLEND),s.disable(s.CULL_FACE),s.disable(s.DEPTH_TEST),s.disable(s.POLYGON_OFFSET_FILL),s.disable(s.SCISSOR_TEST),s.disable(s.STENCIL_TEST),s.disable(s.SAMPLE_ALPHA_TO_COVERAGE),s.blendEquation(s.FUNC_ADD),s.blendFunc(s.ONE,s.ZERO),s.blendFuncSeparate(s.ONE,s.ZERO,s.ONE,s.ZERO),s.blendColor(0,0,0,0),s.colorMask(!0,!0,!0,!0),s.clearColor(0,0,0,0),s.depthMask(!0),s.depthFunc(s.LESS),a.setReversed(!1),s.clearDepth(1),s.stencilMask(4294967295),s.stencilFunc(s.ALWAYS,0,4294967295),s.stencilOp(s.KEEP,s.KEEP,s.KEEP),s.clearStencil(0),s.cullFace(s.BACK),s.frontFace(s.CCW),s.polygonOffset(0,0),s.activeTexture(s.TEXTURE0),s.bindFramebuffer(s.FRAMEBUFFER,null),s.bindFramebuffer(s.DRAW_FRAMEBUFFER,null),s.bindFramebuffer(s.READ_FRAMEBUFFER,null),s.useProgram(null),s.lineWidth(1),s.scissor(0,0,s.canvas.width,s.canvas.height),s.viewport(0,0,s.canvas.width,s.canvas.height),h={},rt=null,ct={},d={},u=new WeakMap,p=[],g=null,v=!1,m=null,f=null,E=null,b=null,S=null,w=null,A=null,C=new Ft(0,0,0),D=0,y=!1,x=null,L=null,N=null,k=null,X=null,te.set(0,0,s.canvas.width,s.canvas.height),se.set(0,0,s.canvas.width,s.canvas.height),r.reset(),a.reset(),o.reset()}return{buffers:{color:r,depth:a,stencil:o},enable:$,disable:ft,bindFramebuffer:Pt,drawBuffers:St,useProgram:Vt,setBlending:re,setMaterial:It,setFlipSided:Ct,setCullFace:gt,setLineWidth:ae,setPolygonOffset:_t,setScissorTest:Ot,activeTexture:xe,bindTexture:he,unbindTexture:T,compressedTexImage2D:_,compressedTexImage3D:O,texImage2D:vt,texImage3D:tt,updateUBOMapping:yt,uniformBlockBinding:at,texStorage2D:nt,texStorage3D:xt,texSubImage2D:G,texSubImage3D:K,compressedTexSubImage2D:V,compressedTexSubImage3D:Mt,scissor:lt,viewport:At,reset:Ut}}function bm(s,t,e,n,i,r,a){const o=t.has("WEBGL_multisampled_render_to_texture")?t.get("WEBGL_multisampled_render_to_texture"):null,l=typeof navigator>"u"?!1:/OculusBrowser/g.test(navigator.userAgent),c=new dt,h=new WeakMap;let d;const u=new WeakMap;let p=!1;try{p=typeof OffscreenCanvas<"u"&&new OffscreenCanvas(1,1).getContext("2d")!==null}catch{}function g(T,_){return p?new OffscreenCanvas(T,_):Bs("canvas")}function v(T,_,O){let G=1;const K=he(T);if((K.width>O||K.height>O)&&(G=O/Math.max(K.width,K.height)),G<1)if(typeof HTMLImageElement<"u"&&T instanceof HTMLImageElement||typeof HTMLCanvasElement<"u"&&T instanceof HTMLCanvasElement||typeof ImageBitmap<"u"&&T instanceof ImageBitmap||typeof VideoFrame<"u"&&T instanceof VideoFrame){const V=Math.floor(G*K.width),Mt=Math.floor(G*K.height);d===void 0&&(d=g(V,Mt));const nt=_?g(V,Mt):d;return nt.width=V,nt.height=Mt,nt.getContext("2d").drawImage(T,0,0,V,Mt),console.warn("THREE.WebGLRenderer: Texture has been resized from ("+K.width+"x"+K.height+") to ("+V+"x"+Mt+")."),nt}else return"data"in T&&console.warn("THREE.WebGLRenderer: Image in DataTexture is too big ("+K.width+"x"+K.height+")."),T;return T}function m(T){return T.generateMipmaps}function f(T){s.generateMipmap(T)}function E(T){return T.isWebGLCubeRenderTarget?s.TEXTURE_CUBE_MAP:T.isWebGL3DRenderTarget?s.TEXTURE_3D:T.isWebGLArrayRenderTarget||T.isCompressedArrayTexture?s.TEXTURE_2D_ARRAY:s.TEXTURE_2D}function b(T,_,O,G,K=!1){if(T!==null){if(s[T]!==void 0)return s[T];console.warn("THREE.WebGLRenderer: Attempt to use non-existing WebGL internal format '"+T+"'")}let V=_;if(_===s.RED&&(O===s.FLOAT&&(V=s.R32F),O===s.HALF_FLOAT&&(V=s.R16F),O===s.UNSIGNED_BYTE&&(V=s.R8)),_===s.RED_INTEGER&&(O===s.UNSIGNED_BYTE&&(V=s.R8UI),O===s.UNSIGNED_SHORT&&(V=s.R16UI),O===s.UNSIGNED_INT&&(V=s.R32UI),O===s.BYTE&&(V=s.R8I),O===s.SHORT&&(V=s.R16I),O===s.INT&&(V=s.R32I)),_===s.RG&&(O===s.FLOAT&&(V=s.RG32F),O===s.HALF_FLOAT&&(V=s.RG16F),O===s.UNSIGNED_BYTE&&(V=s.RG8)),_===s.RG_INTEGER&&(O===s.UNSIGNED_BYTE&&(V=s.RG8UI),O===s.UNSIGNED_SHORT&&(V=s.RG16UI),O===s.UNSIGNED_INT&&(V=s.RG32UI),O===s.BYTE&&(V=s.RG8I),O===s.SHORT&&(V=s.RG16I),O===s.INT&&(V=s.RG32I)),_===s.RGB_INTEGER&&(O===s.UNSIGNED_BYTE&&(V=s.RGB8UI),O===s.UNSIGNED_SHORT&&(V=s.RGB16UI),O===s.UNSIGNED_INT&&(V=s.RGB32UI),O===s.BYTE&&(V=s.RGB8I),O===s.SHORT&&(V=s.RGB16I),O===s.INT&&(V=s.RGB32I)),_===s.RGBA_INTEGER&&(O===s.UNSIGNED_BYTE&&(V=s.RGBA8UI),O===s.UNSIGNED_SHORT&&(V=s.RGBA16UI),O===s.UNSIGNED_INT&&(V=s.RGBA32UI),O===s.BYTE&&(V=s.RGBA8I),O===s.SHORT&&(V=s.RGBA16I),O===s.INT&&(V=s.RGBA32I)),_===s.RGB&&(O===s.UNSIGNED_INT_5_9_9_9_REV&&(V=s.RGB9_E5),O===s.UNSIGNED_INT_10F_11F_11F_REV&&(V=s.R11F_G11F_B10F)),_===s.RGBA){const Mt=K?Fs:Gt.getTransfer(G);O===s.FLOAT&&(V=s.RGBA32F),O===s.HALF_FLOAT&&(V=s.RGBA16F),O===s.UNSIGNED_BYTE&&(V=Mt===jt?s.SRGB8_ALPHA8:s.RGBA8),O===s.UNSIGNED_SHORT_4_4_4_4&&(V=s.RGBA4),O===s.UNSIGNED_SHORT_5_5_5_1&&(V=s.RGB5_A1)}return(V===s.R16F||V===s.R32F||V===s.RG16F||V===s.RG32F||V===s.RGBA16F||V===s.RGBA32F)&&t.get("EXT_color_buffer_float"),V}function S(T,_){let O;return T?_===null||_===jn||_===Xi?O=s.DEPTH24_STENCIL8:_===an?O=s.DEPTH32F_STENCIL8:_===Wi&&(O=s.DEPTH24_STENCIL8,console.warn("DepthTexture: 16 bit depth attachment is not supported with stencil. Using 24-bit attachment.")):_===null||_===jn||_===Xi?O=s.DEPTH_COMPONENT24:_===an?O=s.DEPTH_COMPONENT32F:_===Wi&&(O=s.DEPTH_COMPONENT16),O}function w(T,_){return m(T)===!0||T.isFramebufferTexture&&T.minFilter!==ze&&T.minFilter!==rn?Math.log2(Math.max(_.width,_.height))+1:T.mipmaps!==void 0&&T.mipmaps.length>0?T.mipmaps.length:T.isCompressedTexture&&Array.isArray(T.image)?_.mipmaps.length:1}function A(T){const _=T.target;_.removeEventListener("dispose",A),D(_),_.isVideoTexture&&h.delete(_)}function C(T){const _=T.target;_.removeEventListener("dispose",C),x(_)}function D(T){const _=n.get(T);if(_.__webglInit===void 0)return;const O=T.source,G=u.get(O);if(G){const K=G[_.__cacheKey];K.usedTimes--,K.usedTimes===0&&y(T),Object.keys(G).length===0&&u.delete(O)}n.remove(T)}function y(T){const _=n.get(T);s.deleteTexture(_.__webglTexture);const O=T.source,G=u.get(O);delete G[_.__cacheKey],a.memory.textures--}function x(T){const _=n.get(T);if(T.depthTexture&&(T.depthTexture.dispose(),n.remove(T.depthTexture)),T.isWebGLCubeRenderTarget)for(let G=0;G<6;G++){if(Array.isArray(_.__webglFramebuffer[G]))for(let K=0;K<_.__webglFramebuffer[G].length;K++)s.deleteFramebuffer(_.__webglFramebuffer[G][K]);else s.deleteFramebuffer(_.__webglFramebuffer[G]);_.__webglDepthbuffer&&s.deleteRenderbuffer(_.__webglDepthbuffer[G])}else{if(Array.isArray(_.__webglFramebuffer))for(let G=0;G<_.__webglFramebuffer.length;G++)s.deleteFramebuffer(_.__webglFramebuffer[G]);else s.deleteFramebuffer(_.__webglFramebuffer);if(_.__webglDepthbuffer&&s.deleteRenderbuffer(_.__webglDepthbuffer),_.__webglMultisampledFramebuffer&&s.deleteFramebuffer(_.__webglMultisampledFramebuffer),_.__webglColorRenderbuffer)for(let G=0;G<_.__webglColorRenderbuffer.length;G++)_.__webglColorRenderbuffer[G]&&s.deleteRenderbuffer(_.__webglColorRenderbuffer[G]);_.__webglDepthRenderbuffer&&s.deleteRenderbuffer(_.__webglDepthRenderbuffer)}const O=T.textures;for(let G=0,K=O.length;G<K;G++){const V=n.get(O[G]);V.__webglTexture&&(s.deleteTexture(V.__webglTexture),a.memory.textures--),n.remove(O[G])}n.remove(T)}let L=0;function N(){L=0}function k(){const T=L;return T>=i.maxTextures&&console.warn("THREE.WebGLTextures: Trying to use "+T+" texture units while this GPU supports only "+i.maxTextures),L+=1,T}function X(T){const _=[];return _.push(T.wrapS),_.push(T.wrapT),_.push(T.wrapR||0),_.push(T.magFilter),_.push(T.minFilter),_.push(T.anisotropy),_.push(T.internalFormat),_.push(T.format),_.push(T.type),_.push(T.generateMipmaps),_.push(T.premultiplyAlpha),_.push(T.flipY),_.push(T.unpackAlignment),_.push(T.colorSpace),_.join()}function W(T,_){const O=n.get(T);if(T.isVideoTexture&&Ot(T),T.isRenderTargetTexture===!1&&T.isExternalTexture!==!0&&T.version>0&&O.__version!==T.version){const G=T.image;if(G===null)console.warn("THREE.WebGLRenderer: Texture marked for update but no image data found.");else if(G.complete===!1)console.warn("THREE.WebGLRenderer: Texture marked for update but image is incomplete");else{Y(O,T,_);return}}else T.isExternalTexture&&(O.__webglTexture=T.sourceTexture?T.sourceTexture:null);e.bindTexture(s.TEXTURE_2D,O.__webglTexture,s.TEXTURE0+_)}function q(T,_){const O=n.get(T);if(T.isRenderTargetTexture===!1&&T.version>0&&O.__version!==T.version){Y(O,T,_);return}e.bindTexture(s.TEXTURE_2D_ARRAY,O.__webglTexture,s.TEXTURE0+_)}function Z(T,_){const O=n.get(T);if(T.isRenderTargetTexture===!1&&T.version>0&&O.__version!==T.version){Y(O,T,_);return}e.bindTexture(s.TEXTURE_3D,O.__webglTexture,s.TEXTURE0+_)}function H(T,_){const O=n.get(T);if(T.version>0&&O.__version!==T.version){$(O,T,_);return}e.bindTexture(s.TEXTURE_CUBE_MAP,O.__webglTexture,s.TEXTURE0+_)}const rt={[Vr]:s.REPEAT,[Wn]:s.CLAMP_TO_EDGE,[Gr]:s.MIRRORED_REPEAT},ct={[ze]:s.NEAREST,[Cc]:s.NEAREST_MIPMAP_NEAREST,[es]:s.NEAREST_MIPMAP_LINEAR,[rn]:s.LINEAR,[Ys]:s.LINEAR_MIPMAP_NEAREST,[Xn]:s.LINEAR_MIPMAP_LINEAR},Et={[Dc]:s.NEVER,[Bc]:s.ALWAYS,[Ic]:s.LESS,[Dl]:s.LEQUAL,[Uc]:s.EQUAL,[Oc]:s.GEQUAL,[Nc]:s.GREATER,[Fc]:s.NOTEQUAL};function kt(T,_){if(_.type===an&&t.has("OES_texture_float_linear")===!1&&(_.magFilter===rn||_.magFilter===Ys||_.magFilter===es||_.magFilter===Xn||_.minFilter===rn||_.minFilter===Ys||_.minFilter===es||_.minFilter===Xn)&&console.warn("THREE.WebGLRenderer: Unable to use linear filtering with floating point textures. OES_texture_float_linear not supported on this device."),s.texParameteri(T,s.TEXTURE_WRAP_S,rt[_.wrapS]),s.texParameteri(T,s.TEXTURE_WRAP_T,rt[_.wrapT]),(T===s.TEXTURE_3D||T===s.TEXTURE_2D_ARRAY)&&s.texParameteri(T,s.TEXTURE_WRAP_R,rt[_.wrapR]),s.texParameteri(T,s.TEXTURE_MAG_FILTER,ct[_.magFilter]),s.texParameteri(T,s.TEXTURE_MIN_FILTER,ct[_.minFilter]),_.compareFunction&&(s.texParameteri(T,s.TEXTURE_COMPARE_MODE,s.COMPARE_REF_TO_TEXTURE),s.texParameteri(T,s.TEXTURE_COMPARE_FUNC,Et[_.compareFunction])),t.has("EXT_texture_filter_anisotropic")===!0){if(_.magFilter===ze||_.minFilter!==es&&_.minFilter!==Xn||_.type===an&&t.has("OES_texture_float_linear")===!1)return;if(_.anisotropy>1||n.get(_).__currentAnisotropy){const O=t.get("EXT_texture_filter_anisotropic");s.texParameterf(T,O.TEXTURE_MAX_ANISOTROPY_EXT,Math.min(_.anisotropy,i.getMaxAnisotropy())),n.get(_).__currentAnisotropy=_.anisotropy}}}function te(T,_){let O=!1;T.__webglInit===void 0&&(T.__webglInit=!0,_.addEventListener("dispose",A));const G=_.source;let K=u.get(G);K===void 0&&(K={},u.set(G,K));const V=X(_);if(V!==T.__cacheKey){K[V]===void 0&&(K[V]={texture:s.createTexture(),usedTimes:0},a.memory.textures++,O=!0),K[V].usedTimes++;const Mt=K[T.__cacheKey];Mt!==void 0&&(K[T.__cacheKey].usedTimes--,Mt.usedTimes===0&&y(_)),T.__cacheKey=V,T.__webglTexture=K[V].texture}return O}function se(T,_,O){return Math.floor(Math.floor(T/O)/_)}function qt(T,_,O,G){const V=T.updateRanges;if(V.length===0)e.texSubImage2D(s.TEXTURE_2D,0,0,0,_.width,_.height,O,G,_.data);else{V.sort((tt,lt)=>tt.start-lt.start);let Mt=0;for(let tt=1;tt<V.length;tt++){const lt=V[Mt],At=V[tt],yt=lt.start+lt.count,at=se(At.start,_.width,4),Ut=se(lt.start,_.width,4);At.start<=yt+1&&at===Ut&&se(At.start+At.count-1,_.width,4)===at?lt.count=Math.max(lt.count,At.start+At.count-lt.start):(++Mt,V[Mt]=At)}V.length=Mt+1;const nt=s.getParameter(s.UNPACK_ROW_LENGTH),xt=s.getParameter(s.UNPACK_SKIP_PIXELS),vt=s.getParameter(s.UNPACK_SKIP_ROWS);s.pixelStorei(s.UNPACK_ROW_LENGTH,_.width);for(let tt=0,lt=V.length;tt<lt;tt++){const At=V[tt],yt=Math.floor(At.start/4),at=Math.ceil(At.count/4),Ut=yt%_.width,I=Math.floor(yt/_.width),et=at,it=1;s.pixelStorei(s.UNPACK_SKIP_PIXELS,Ut),s.pixelStorei(s.UNPACK_SKIP_ROWS,I),e.texSubImage2D(s.TEXTURE_2D,0,Ut,I,et,it,O,G,_.data)}T.clearUpdateRanges(),s.pixelStorei(s.UNPACK_ROW_LENGTH,nt),s.pixelStorei(s.UNPACK_SKIP_PIXELS,xt),s.pixelStorei(s.UNPACK_SKIP_ROWS,vt)}}function Y(T,_,O){let G=s.TEXTURE_2D;(_.isDataArrayTexture||_.isCompressedArrayTexture)&&(G=s.TEXTURE_2D_ARRAY),_.isData3DTexture&&(G=s.TEXTURE_3D);const K=te(T,_),V=_.source;e.bindTexture(G,T.__webglTexture,s.TEXTURE0+O);const Mt=n.get(V);if(V.version!==Mt.__version||K===!0){e.activeTexture(s.TEXTURE0+O);const nt=Gt.getPrimaries(Gt.workingColorSpace),xt=_.colorSpace===Rn?null:Gt.getPrimaries(_.colorSpace),vt=_.colorSpace===Rn||nt===xt?s.NONE:s.BROWSER_DEFAULT_WEBGL;s.pixelStorei(s.UNPACK_FLIP_Y_WEBGL,_.flipY),s.pixelStorei(s.UNPACK_PREMULTIPLY_ALPHA_WEBGL,_.premultiplyAlpha),s.pixelStorei(s.UNPACK_ALIGNMENT,_.unpackAlignment),s.pixelStorei(s.UNPACK_COLORSPACE_CONVERSION_WEBGL,vt);let tt=v(_.image,!1,i.maxTextureSize);tt=xe(_,tt);const lt=r.convert(_.format,_.colorSpace),At=r.convert(_.type);let yt=b(_.internalFormat,lt,At,_.colorSpace,_.isVideoTexture);kt(G,_);let at;const Ut=_.mipmaps,I=_.isVideoTexture!==!0,et=Mt.__version===void 0||K===!0,it=V.dataReady,ut=w(_,tt);if(_.isDepthTexture)yt=S(_.format===Yi,_.type),et&&(I?e.texStorage2D(s.TEXTURE_2D,1,yt,tt.width,tt.height):e.texImage2D(s.TEXTURE_2D,0,yt,tt.width,tt.height,0,lt,At,null));else if(_.isDataTexture)if(Ut.length>0){I&&et&&e.texStorage2D(s.TEXTURE_2D,ut,yt,Ut[0].width,Ut[0].height);for(let J=0,j=Ut.length;J<j;J++)at=Ut[J],I?it&&e.texSubImage2D(s.TEXTURE_2D,J,0,0,at.width,at.height,lt,At,at.data):e.texImage2D(s.TEXTURE_2D,J,yt,at.width,at.height,0,lt,At,at.data);_.generateMipmaps=!1}else I?(et&&e.texStorage2D(s.TEXTURE_2D,ut,yt,tt.width,tt.height),it&&qt(_,tt,lt,At)):e.texImage2D(s.TEXTURE_2D,0,yt,tt.width,tt.height,0,lt,At,tt.data);else if(_.isCompressedTexture)if(_.isCompressedArrayTexture){I&&et&&e.texStorage3D(s.TEXTURE_2D_ARRAY,ut,yt,Ut[0].width,Ut[0].height,tt.depth);for(let J=0,j=Ut.length;J<j;J++)if(at=Ut[J],_.format!==Ze)if(lt!==null)if(I){if(it)if(_.layerUpdates.size>0){const mt=Fo(at.width,at.height,_.format,_.type);for(const Lt of _.layerUpdates){const ee=at.data.subarray(Lt*mt/at.data.BYTES_PER_ELEMENT,(Lt+1)*mt/at.data.BYTES_PER_ELEMENT);e.compressedTexSubImage3D(s.TEXTURE_2D_ARRAY,J,0,0,Lt,at.width,at.height,1,lt,ee)}_.clearLayerUpdates()}else e.compressedTexSubImage3D(s.TEXTURE_2D_ARRAY,J,0,0,0,at.width,at.height,tt.depth,lt,at.data)}else e.compressedTexImage3D(s.TEXTURE_2D_ARRAY,J,yt,at.width,at.height,tt.depth,0,at.data,0,0);else console.warn("THREE.WebGLRenderer: Attempt to load unsupported compressed texture format in .uploadTexture()");else I?it&&e.texSubImage3D(s.TEXTURE_2D_ARRAY,J,0,0,0,at.width,at.height,tt.depth,lt,At,at.data):e.texImage3D(s.TEXTURE_2D_ARRAY,J,yt,at.width,at.height,tt.depth,0,lt,At,at.data)}else{I&&et&&e.texStorage2D(s.TEXTURE_2D,ut,yt,Ut[0].width,Ut[0].height);for(let J=0,j=Ut.length;J<j;J++)at=Ut[J],_.format!==Ze?lt!==null?I?it&&e.compressedTexSubImage2D(s.TEXTURE_2D,J,0,0,at.width,at.height,lt,at.data):e.compressedTexImage2D(s.TEXTURE_2D,J,yt,at.width,at.height,0,at.data):console.warn("THREE.WebGLRenderer: Attempt to load unsupported compressed texture format in .uploadTexture()"):I?it&&e.texSubImage2D(s.TEXTURE_2D,J,0,0,at.width,at.height,lt,At,at.data):e.texImage2D(s.TEXTURE_2D,J,yt,at.width,at.height,0,lt,At,at.data)}else if(_.isDataArrayTexture)if(I){if(et&&e.texStorage3D(s.TEXTURE_2D_ARRAY,ut,yt,tt.width,tt.height,tt.depth),it)if(_.layerUpdates.size>0){const J=Fo(tt.width,tt.height,_.format,_.type);for(const j of _.layerUpdates){const mt=tt.data.subarray(j*J/tt.data.BYTES_PER_ELEMENT,(j+1)*J/tt.data.BYTES_PER_ELEMENT);e.texSubImage3D(s.TEXTURE_2D_ARRAY,0,0,0,j,tt.width,tt.height,1,lt,At,mt)}_.clearLayerUpdates()}else e.texSubImage3D(s.TEXTURE_2D_ARRAY,0,0,0,0,tt.width,tt.height,tt.depth,lt,At,tt.data)}else e.texImage3D(s.TEXTURE_2D_ARRAY,0,yt,tt.width,tt.height,tt.depth,0,lt,At,tt.data);else if(_.isData3DTexture)I?(et&&e.texStorage3D(s.TEXTURE_3D,ut,yt,tt.width,tt.height,tt.depth),it&&e.texSubImage3D(s.TEXTURE_3D,0,0,0,0,tt.width,tt.height,tt.depth,lt,At,tt.data)):e.texImage3D(s.TEXTURE_3D,0,yt,tt.width,tt.height,tt.depth,0,lt,At,tt.data);else if(_.isFramebufferTexture){if(et)if(I)e.texStorage2D(s.TEXTURE_2D,ut,yt,tt.width,tt.height);else{let J=tt.width,j=tt.height;for(let mt=0;mt<ut;mt++)e.texImage2D(s.TEXTURE_2D,mt,yt,J,j,0,lt,At,null),J>>=1,j>>=1}}else if(Ut.length>0){if(I&&et){const J=he(Ut[0]);e.texStorage2D(s.TEXTURE_2D,ut,yt,J.width,J.height)}for(let J=0,j=Ut.length;J<j;J++)at=Ut[J],I?it&&e.texSubImage2D(s.TEXTURE_2D,J,0,0,lt,At,at):e.texImage2D(s.TEXTURE_2D,J,yt,lt,At,at);_.generateMipmaps=!1}else if(I){if(et){const J=he(tt);e.texStorage2D(s.TEXTURE_2D,ut,yt,J.width,J.height)}it&&e.texSubImage2D(s.TEXTURE_2D,0,0,0,lt,At,tt)}else e.texImage2D(s.TEXTURE_2D,0,yt,lt,At,tt);m(_)&&f(G),Mt.__version=V.version,_.onUpdate&&_.onUpdate(_)}T.__version=_.version}function $(T,_,O){if(_.image.length!==6)return;const G=te(T,_),K=_.source;e.bindTexture(s.TEXTURE_CUBE_MAP,T.__webglTexture,s.TEXTURE0+O);const V=n.get(K);if(K.version!==V.__version||G===!0){e.activeTexture(s.TEXTURE0+O);const Mt=Gt.getPrimaries(Gt.workingColorSpace),nt=_.colorSpace===Rn?null:Gt.getPrimaries(_.colorSpace),xt=_.colorSpace===Rn||Mt===nt?s.NONE:s.BROWSER_DEFAULT_WEBGL;s.pixelStorei(s.UNPACK_FLIP_Y_WEBGL,_.flipY),s.pixelStorei(s.UNPACK_PREMULTIPLY_ALPHA_WEBGL,_.premultiplyAlpha),s.pixelStorei(s.UNPACK_ALIGNMENT,_.unpackAlignment),s.pixelStorei(s.UNPACK_COLORSPACE_CONVERSION_WEBGL,xt);const vt=_.isCompressedTexture||_.image[0].isCompressedTexture,tt=_.image[0]&&_.image[0].isDataTexture,lt=[];for(let j=0;j<6;j++)!vt&&!tt?lt[j]=v(_.image[j],!0,i.maxCubemapSize):lt[j]=tt?_.image[j].image:_.image[j],lt[j]=xe(_,lt[j]);const At=lt[0],yt=r.convert(_.format,_.colorSpace),at=r.convert(_.type),Ut=b(_.internalFormat,yt,at,_.colorSpace),I=_.isVideoTexture!==!0,et=V.__version===void 0||G===!0,it=K.dataReady;let ut=w(_,At);kt(s.TEXTURE_CUBE_MAP,_);let J;if(vt){I&&et&&e.texStorage2D(s.TEXTURE_CUBE_MAP,ut,Ut,At.width,At.height);for(let j=0;j<6;j++){J=lt[j].mipmaps;for(let mt=0;mt<J.length;mt++){const Lt=J[mt];_.format!==Ze?yt!==null?I?it&&e.compressedTexSubImage2D(s.TEXTURE_CUBE_MAP_POSITIVE_X+j,mt,0,0,Lt.width,Lt.height,yt,Lt.data):e.compressedTexImage2D(s.TEXTURE_CUBE_MAP_POSITIVE_X+j,mt,Ut,Lt.width,Lt.height,0,Lt.data):console.warn("THREE.WebGLRenderer: Attempt to load unsupported compressed texture format in .setTextureCube()"):I?it&&e.texSubImage2D(s.TEXTURE_CUBE_MAP_POSITIVE_X+j,mt,0,0,Lt.width,Lt.height,yt,at,Lt.data):e.texImage2D(s.TEXTURE_CUBE_MAP_POSITIVE_X+j,mt,Ut,Lt.width,Lt.height,0,yt,at,Lt.data)}}}else{if(J=_.mipmaps,I&&et){J.length>0&&ut++;const j=he(lt[0]);e.texStorage2D(s.TEXTURE_CUBE_MAP,ut,Ut,j.width,j.height)}for(let j=0;j<6;j++)if(tt){I?it&&e.texSubImage2D(s.TEXTURE_CUBE_MAP_POSITIVE_X+j,0,0,0,lt[j].width,lt[j].height,yt,at,lt[j].data):e.texImage2D(s.TEXTURE_CUBE_MAP_POSITIVE_X+j,0,Ut,lt[j].width,lt[j].height,0,yt,at,lt[j].data);for(let mt=0;mt<J.length;mt++){const ee=J[mt].image[j].image;I?it&&e.texSubImage2D(s.TEXTURE_CUBE_MAP_POSITIVE_X+j,mt+1,0,0,ee.width,ee.height,yt,at,ee.data):e.texImage2D(s.TEXTURE_CUBE_MAP_POSITIVE_X+j,mt+1,Ut,ee.width,ee.height,0,yt,at,ee.data)}}else{I?it&&e.texSubImage2D(s.TEXTURE_CUBE_MAP_POSITIVE_X+j,0,0,0,yt,at,lt[j]):e.texImage2D(s.TEXTURE_CUBE_MAP_POSITIVE_X+j,0,Ut,yt,at,lt[j]);for(let mt=0;mt<J.length;mt++){const Lt=J[mt];I?it&&e.texSubImage2D(s.TEXTURE_CUBE_MAP_POSITIVE_X+j,mt+1,0,0,yt,at,Lt.image[j]):e.texImage2D(s.TEXTURE_CUBE_MAP_POSITIVE_X+j,mt+1,Ut,yt,at,Lt.image[j])}}}m(_)&&f(s.TEXTURE_CUBE_MAP),V.__version=K.version,_.onUpdate&&_.onUpdate(_)}T.__version=_.version}function ft(T,_,O,G,K,V){const Mt=r.convert(O.format,O.colorSpace),nt=r.convert(O.type),xt=b(O.internalFormat,Mt,nt,O.colorSpace),vt=n.get(_),tt=n.get(O);if(tt.__renderTarget=_,!vt.__hasExternalTextures){const lt=Math.max(1,_.width>>V),At=Math.max(1,_.height>>V);K===s.TEXTURE_3D||K===s.TEXTURE_2D_ARRAY?e.texImage3D(K,V,xt,lt,At,_.depth,0,Mt,nt,null):e.texImage2D(K,V,xt,lt,At,0,Mt,nt,null)}e.bindFramebuffer(s.FRAMEBUFFER,T),_t(_)?o.framebufferTexture2DMultisampleEXT(s.FRAMEBUFFER,G,K,tt.__webglTexture,0,ae(_)):(K===s.TEXTURE_2D||K>=s.TEXTURE_CUBE_MAP_POSITIVE_X&&K<=s.TEXTURE_CUBE_MAP_NEGATIVE_Z)&&s.framebufferTexture2D(s.FRAMEBUFFER,G,K,tt.__webglTexture,V),e.bindFramebuffer(s.FRAMEBUFFER,null)}function Pt(T,_,O){if(s.bindRenderbuffer(s.RENDERBUFFER,T),_.depthBuffer){const G=_.depthTexture,K=G&&G.isDepthTexture?G.type:null,V=S(_.stencilBuffer,K),Mt=_.stencilBuffer?s.DEPTH_STENCIL_ATTACHMENT:s.DEPTH_ATTACHMENT,nt=ae(_);_t(_)?o.renderbufferStorageMultisampleEXT(s.RENDERBUFFER,nt,V,_.width,_.height):O?s.renderbufferStorageMultisample(s.RENDERBUFFER,nt,V,_.width,_.height):s.renderbufferStorage(s.RENDERBUFFER,V,_.width,_.height),s.framebufferRenderbuffer(s.FRAMEBUFFER,Mt,s.RENDERBUFFER,T)}else{const G=_.textures;for(let K=0;K<G.length;K++){const V=G[K],Mt=r.convert(V.format,V.colorSpace),nt=r.convert(V.type),xt=b(V.internalFormat,Mt,nt,V.colorSpace),vt=ae(_);O&&_t(_)===!1?s.renderbufferStorageMultisample(s.RENDERBUFFER,vt,xt,_.width,_.height):_t(_)?o.renderbufferStorageMultisampleEXT(s.RENDERBUFFER,vt,xt,_.width,_.height):s.renderbufferStorage(s.RENDERBUFFER,xt,_.width,_.height)}}s.bindRenderbuffer(s.RENDERBUFFER,null)}function St(T,_){if(_&&_.isWebGLCubeRenderTarget)throw new Error("Depth Texture with cube render targets is not supported");if(e.bindFramebuffer(s.FRAMEBUFFER,T),!(_.depthTexture&&_.depthTexture.isDepthTexture))throw new Error("renderTarget.depthTexture must be an instance of THREE.DepthTexture");const G=n.get(_.depthTexture);G.__renderTarget=_,(!G.__webglTexture||_.depthTexture.image.width!==_.width||_.depthTexture.image.height!==_.height)&&(_.depthTexture.image.width=_.width,_.depthTexture.image.height=_.height,_.depthTexture.needsUpdate=!0),W(_.depthTexture,0);const K=G.__webglTexture,V=ae(_);if(_.depthTexture.format===qi)_t(_)?o.framebufferTexture2DMultisampleEXT(s.FRAMEBUFFER,s.DEPTH_ATTACHMENT,s.TEXTURE_2D,K,0,V):s.framebufferTexture2D(s.FRAMEBUFFER,s.DEPTH_ATTACHMENT,s.TEXTURE_2D,K,0);else if(_.depthTexture.format===Yi)_t(_)?o.framebufferTexture2DMultisampleEXT(s.FRAMEBUFFER,s.DEPTH_STENCIL_ATTACHMENT,s.TEXTURE_2D,K,0,V):s.framebufferTexture2D(s.FRAMEBUFFER,s.DEPTH_STENCIL_ATTACHMENT,s.TEXTURE_2D,K,0);else throw new Error("Unknown depthTexture format")}function Vt(T){const _=n.get(T),O=T.isWebGLCubeRenderTarget===!0;if(_.__boundDepthTexture!==T.depthTexture){const G=T.depthTexture;if(_.__depthDisposeCallback&&_.__depthDisposeCallback(),G){const K=()=>{delete _.__boundDepthTexture,delete _.__depthDisposeCallback,G.removeEventListener("dispose",K)};G.addEventListener("dispose",K),_.__depthDisposeCallback=K}_.__boundDepthTexture=G}if(T.depthTexture&&!_.__autoAllocateDepthBuffer){if(O)throw new Error("target.depthTexture not supported in Cube render targets");const G=T.texture.mipmaps;G&&G.length>0?St(_.__webglFramebuffer[0],T):St(_.__webglFramebuffer,T)}else if(O){_.__webglDepthbuffer=[];for(let G=0;G<6;G++)if(e.bindFramebuffer(s.FRAMEBUFFER,_.__webglFramebuffer[G]),_.__webglDepthbuffer[G]===void 0)_.__webglDepthbuffer[G]=s.createRenderbuffer(),Pt(_.__webglDepthbuffer[G],T,!1);else{const K=T.stencilBuffer?s.DEPTH_STENCIL_ATTACHMENT:s.DEPTH_ATTACHMENT,V=_.__webglDepthbuffer[G];s.bindRenderbuffer(s.RENDERBUFFER,V),s.framebufferRenderbuffer(s.FRAMEBUFFER,K,s.RENDERBUFFER,V)}}else{const G=T.texture.mipmaps;if(G&&G.length>0?e.bindFramebuffer(s.FRAMEBUFFER,_.__webglFramebuffer[0]):e.bindFramebuffer(s.FRAMEBUFFER,_.__webglFramebuffer),_.__webglDepthbuffer===void 0)_.__webglDepthbuffer=s.createRenderbuffer(),Pt(_.__webglDepthbuffer,T,!1);else{const K=T.stencilBuffer?s.DEPTH_STENCIL_ATTACHMENT:s.DEPTH_ATTACHMENT,V=_.__webglDepthbuffer;s.bindRenderbuffer(s.RENDERBUFFER,V),s.framebufferRenderbuffer(s.FRAMEBUFFER,K,s.RENDERBUFFER,V)}}e.bindFramebuffer(s.FRAMEBUFFER,null)}function Se(T,_,O){const G=n.get(T);_!==void 0&&ft(G.__webglFramebuffer,T,T.texture,s.COLOR_ATTACHMENT0,s.TEXTURE_2D,0),O!==void 0&&Vt(T)}function R(T){const _=T.texture,O=n.get(T),G=n.get(_);T.addEventListener("dispose",C);const K=T.textures,V=T.isWebGLCubeRenderTarget===!0,Mt=K.length>1;if(Mt||(G.__webglTexture===void 0&&(G.__webglTexture=s.createTexture()),G.__version=_.version,a.memory.textures++),V){O.__webglFramebuffer=[];for(let nt=0;nt<6;nt++)if(_.mipmaps&&_.mipmaps.length>0){O.__webglFramebuffer[nt]=[];for(let xt=0;xt<_.mipmaps.length;xt++)O.__webglFramebuffer[nt][xt]=s.createFramebuffer()}else O.__webglFramebuffer[nt]=s.createFramebuffer()}else{if(_.mipmaps&&_.mipmaps.length>0){O.__webglFramebuffer=[];for(let nt=0;nt<_.mipmaps.length;nt++)O.__webglFramebuffer[nt]=s.createFramebuffer()}else O.__webglFramebuffer=s.createFramebuffer();if(Mt)for(let nt=0,xt=K.length;nt<xt;nt++){const vt=n.get(K[nt]);vt.__webglTexture===void 0&&(vt.__webglTexture=s.createTexture(),a.memory.textures++)}if(T.samples>0&&_t(T)===!1){O.__webglMultisampledFramebuffer=s.createFramebuffer(),O.__webglColorRenderbuffer=[],e.bindFramebuffer(s.FRAMEBUFFER,O.__webglMultisampledFramebuffer);for(let nt=0;nt<K.length;nt++){const xt=K[nt];O.__webglColorRenderbuffer[nt]=s.createRenderbuffer(),s.bindRenderbuffer(s.RENDERBUFFER,O.__webglColorRenderbuffer[nt]);const vt=r.convert(xt.format,xt.colorSpace),tt=r.convert(xt.type),lt=b(xt.internalFormat,vt,tt,xt.colorSpace,T.isXRRenderTarget===!0),At=ae(T);s.renderbufferStorageMultisample(s.RENDERBUFFER,At,lt,T.width,T.height),s.framebufferRenderbuffer(s.FRAMEBUFFER,s.COLOR_ATTACHMENT0+nt,s.RENDERBUFFER,O.__webglColorRenderbuffer[nt])}s.bindRenderbuffer(s.RENDERBUFFER,null),T.depthBuffer&&(O.__webglDepthRenderbuffer=s.createRenderbuffer(),Pt(O.__webglDepthRenderbuffer,T,!0)),e.bindFramebuffer(s.FRAMEBUFFER,null)}}if(V){e.bindTexture(s.TEXTURE_CUBE_MAP,G.__webglTexture),kt(s.TEXTURE_CUBE_MAP,_);for(let nt=0;nt<6;nt++)if(_.mipmaps&&_.mipmaps.length>0)for(let xt=0;xt<_.mipmaps.length;xt++)ft(O.__webglFramebuffer[nt][xt],T,_,s.COLOR_ATTACHMENT0,s.TEXTURE_CUBE_MAP_POSITIVE_X+nt,xt);else ft(O.__webglFramebuffer[nt],T,_,s.COLOR_ATTACHMENT0,s.TEXTURE_CUBE_MAP_POSITIVE_X+nt,0);m(_)&&f(s.TEXTURE_CUBE_MAP),e.unbindTexture()}else if(Mt){for(let nt=0,xt=K.length;nt<xt;nt++){const vt=K[nt],tt=n.get(vt);let lt=s.TEXTURE_2D;(T.isWebGL3DRenderTarget||T.isWebGLArrayRenderTarget)&&(lt=T.isWebGL3DRenderTarget?s.TEXTURE_3D:s.TEXTURE_2D_ARRAY),e.bindTexture(lt,tt.__webglTexture),kt(lt,vt),ft(O.__webglFramebuffer,T,vt,s.COLOR_ATTACHMENT0+nt,lt,0),m(vt)&&f(lt)}e.unbindTexture()}else{let nt=s.TEXTURE_2D;if((T.isWebGL3DRenderTarget||T.isWebGLArrayRenderTarget)&&(nt=T.isWebGL3DRenderTarget?s.TEXTURE_3D:s.TEXTURE_2D_ARRAY),e.bindTexture(nt,G.__webglTexture),kt(nt,_),_.mipmaps&&_.mipmaps.length>0)for(let xt=0;xt<_.mipmaps.length;xt++)ft(O.__webglFramebuffer[xt],T,_,s.COLOR_ATTACHMENT0,nt,xt);else ft(O.__webglFramebuffer,T,_,s.COLOR_ATTACHMENT0,nt,0);m(_)&&f(nt),e.unbindTexture()}T.depthBuffer&&Vt(T)}function re(T){const _=T.textures;for(let O=0,G=_.length;O<G;O++){const K=_[O];if(m(K)){const V=E(T),Mt=n.get(K).__webglTexture;e.bindTexture(V,Mt),f(V),e.unbindTexture()}}}const It=[],Ct=[];function gt(T){if(T.samples>0){if(_t(T)===!1){const _=T.textures,O=T.width,G=T.height;let K=s.COLOR_BUFFER_BIT;const V=T.stencilBuffer?s.DEPTH_STENCIL_ATTACHMENT:s.DEPTH_ATTACHMENT,Mt=n.get(T),nt=_.length>1;if(nt)for(let vt=0;vt<_.length;vt++)e.bindFramebuffer(s.FRAMEBUFFER,Mt.__webglMultisampledFramebuffer),s.framebufferRenderbuffer(s.FRAMEBUFFER,s.COLOR_ATTACHMENT0+vt,s.RENDERBUFFER,null),e.bindFramebuffer(s.FRAMEBUFFER,Mt.__webglFramebuffer),s.framebufferTexture2D(s.DRAW_FRAMEBUFFER,s.COLOR_ATTACHMENT0+vt,s.TEXTURE_2D,null,0);e.bindFramebuffer(s.READ_FRAMEBUFFER,Mt.__webglMultisampledFramebuffer);const xt=T.texture.mipmaps;xt&&xt.length>0?e.bindFramebuffer(s.DRAW_FRAMEBUFFER,Mt.__webglFramebuffer[0]):e.bindFramebuffer(s.DRAW_FRAMEBUFFER,Mt.__webglFramebuffer);for(let vt=0;vt<_.length;vt++){if(T.resolveDepthBuffer&&(T.depthBuffer&&(K|=s.DEPTH_BUFFER_BIT),T.stencilBuffer&&T.resolveStencilBuffer&&(K|=s.STENCIL_BUFFER_BIT)),nt){s.framebufferRenderbuffer(s.READ_FRAMEBUFFER,s.COLOR_ATTACHMENT0,s.RENDERBUFFER,Mt.__webglColorRenderbuffer[vt]);const tt=n.get(_[vt]).__webglTexture;s.framebufferTexture2D(s.DRAW_FRAMEBUFFER,s.COLOR_ATTACHMENT0,s.TEXTURE_2D,tt,0)}s.blitFramebuffer(0,0,O,G,0,0,O,G,K,s.NEAREST),l===!0&&(It.length=0,Ct.length=0,It.push(s.COLOR_ATTACHMENT0+vt),T.depthBuffer&&T.resolveDepthBuffer===!1&&(It.push(V),Ct.push(V),s.invalidateFramebuffer(s.DRAW_FRAMEBUFFER,Ct)),s.invalidateFramebuffer(s.READ_FRAMEBUFFER,It))}if(e.bindFramebuffer(s.READ_FRAMEBUFFER,null),e.bindFramebuffer(s.DRAW_FRAMEBUFFER,null),nt)for(let vt=0;vt<_.length;vt++){e.bindFramebuffer(s.FRAMEBUFFER,Mt.__webglMultisampledFramebuffer),s.framebufferRenderbuffer(s.FRAMEBUFFER,s.COLOR_ATTACHMENT0+vt,s.RENDERBUFFER,Mt.__webglColorRenderbuffer[vt]);const tt=n.get(_[vt]).__webglTexture;e.bindFramebuffer(s.FRAMEBUFFER,Mt.__webglFramebuffer),s.framebufferTexture2D(s.DRAW_FRAMEBUFFER,s.COLOR_ATTACHMENT0+vt,s.TEXTURE_2D,tt,0)}e.bindFramebuffer(s.DRAW_FRAMEBUFFER,Mt.__webglMultisampledFramebuffer)}else if(T.depthBuffer&&T.resolveDepthBuffer===!1&&l){const _=T.stencilBuffer?s.DEPTH_STENCIL_ATTACHMENT:s.DEPTH_ATTACHMENT;s.invalidateFramebuffer(s.DRAW_FRAMEBUFFER,[_])}}}function ae(T){return Math.min(i.maxSamples,T.samples)}function _t(T){const _=n.get(T);return T.samples>0&&t.has("WEBGL_multisampled_render_to_texture")===!0&&_.__useRenderToTexture!==!1}function Ot(T){const _=a.render.frame;h.get(T)!==_&&(h.set(T,_),T.update())}function xe(T,_){const O=T.colorSpace,G=T.format,K=T.type;return T.isCompressedTexture===!0||T.isVideoTexture===!0||O!==Ei&&O!==Rn&&(Gt.getTransfer(O)===jt?(G!==Ze||K!==ln)&&console.warn("THREE.WebGLTextures: sRGB encoded textures have to use RGBAFormat and UnsignedByteType."):console.error("THREE.WebGLTextures: Unsupported texture color space:",O)),_}function he(T){return typeof HTMLImageElement<"u"&&T instanceof HTMLImageElement?(c.width=T.naturalWidth||T.width,c.height=T.naturalHeight||T.height):typeof VideoFrame<"u"&&T instanceof VideoFrame?(c.width=T.displayWidth,c.height=T.displayHeight):(c.width=T.width,c.height=T.height),c}this.allocateTextureUnit=k,this.resetTextureUnits=N,this.setTexture2D=W,this.setTexture2DArray=q,this.setTexture3D=Z,this.setTextureCube=H,this.rebindTextures=Se,this.setupRenderTarget=R,this.updateRenderTargetMipmap=re,this.updateMultisampleRenderTarget=gt,this.setupDepthRenderbuffer=Vt,this.setupFrameBufferTexture=ft,this.useMultisampledRTT=_t}function Tm(s,t){function e(n,i=Rn){let r;const a=Gt.getTransfer(i);if(n===ln)return s.UNSIGNED_BYTE;if(n===ba)return s.UNSIGNED_SHORT_4_4_4_4;if(n===Ta)return s.UNSIGNED_SHORT_5_5_5_1;if(n===wl)return s.UNSIGNED_INT_5_9_9_9_REV;if(n===Al)return s.UNSIGNED_INT_10F_11F_11F_REV;if(n===bl)return s.BYTE;if(n===Tl)return s.SHORT;if(n===Wi)return s.UNSIGNED_SHORT;if(n===Ea)return s.INT;if(n===jn)return s.UNSIGNED_INT;if(n===an)return s.FLOAT;if(n===yn)return s.HALF_FLOAT;if(n===Cl)return s.ALPHA;if(n===Rl)return s.RGB;if(n===Ze)return s.RGBA;if(n===qi)return s.DEPTH_COMPONENT;if(n===Yi)return s.DEPTH_STENCIL;if(n===wa)return s.RED;if(n===Aa)return s.RED_INTEGER;if(n===Pl)return s.RG;if(n===Ca)return s.RG_INTEGER;if(n===Ra)return s.RGBA_INTEGER;if(n===Rs||n===Ps||n===Ls||n===Ds)if(a===jt)if(r=t.get("WEBGL_compressed_texture_s3tc_srgb"),r!==null){if(n===Rs)return r.COMPRESSED_SRGB_S3TC_DXT1_EXT;if(n===Ps)return r.COMPRESSED_SRGB_ALPHA_S3TC_DXT1_EXT;if(n===Ls)return r.COMPRESSED_SRGB_ALPHA_S3TC_DXT3_EXT;if(n===Ds)return r.COMPRESSED_SRGB_ALPHA_S3TC_DXT5_EXT}else return null;else if(r=t.get("WEBGL_compressed_texture_s3tc"),r!==null){if(n===Rs)return r.COMPRESSED_RGB_S3TC_DXT1_EXT;if(n===Ps)return r.COMPRESSED_RGBA_S3TC_DXT1_EXT;if(n===Ls)return r.COMPRESSED_RGBA_S3TC_DXT3_EXT;if(n===Ds)return r.COMPRESSED_RGBA_S3TC_DXT5_EXT}else return null;if(n===Wr||n===Xr||n===qr||n===Yr)if(r=t.get("WEBGL_compressed_texture_pvrtc"),r!==null){if(n===Wr)return r.COMPRESSED_RGB_PVRTC_4BPPV1_IMG;if(n===Xr)return r.COMPRESSED_RGB_PVRTC_2BPPV1_IMG;if(n===qr)return r.COMPRESSED_RGBA_PVRTC_4BPPV1_IMG;if(n===Yr)return r.COMPRESSED_RGBA_PVRTC_2BPPV1_IMG}else return null;if(n===jr||n===Kr||n===Zr)if(r=t.get("WEBGL_compressed_texture_etc"),r!==null){if(n===jr||n===Kr)return a===jt?r.COMPRESSED_SRGB8_ETC2:r.COMPRESSED_RGB8_ETC2;if(n===Zr)return a===jt?r.COMPRESSED_SRGB8_ALPHA8_ETC2_EAC:r.COMPRESSED_RGBA8_ETC2_EAC}else return null;if(n===$r||n===Jr||n===Qr||n===ta||n===ea||n===na||n===ia||n===sa||n===ra||n===aa||n===oa||n===la||n===ca||n===ha)if(r=t.get("WEBGL_compressed_texture_astc"),r!==null){if(n===$r)return a===jt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_4x4_KHR:r.COMPRESSED_RGBA_ASTC_4x4_KHR;if(n===Jr)return a===jt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_5x4_KHR:r.COMPRESSED_RGBA_ASTC_5x4_KHR;if(n===Qr)return a===jt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_5x5_KHR:r.COMPRESSED_RGBA_ASTC_5x5_KHR;if(n===ta)return a===jt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_6x5_KHR:r.COMPRESSED_RGBA_ASTC_6x5_KHR;if(n===ea)return a===jt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_6x6_KHR:r.COMPRESSED_RGBA_ASTC_6x6_KHR;if(n===na)return a===jt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_8x5_KHR:r.COMPRESSED_RGBA_ASTC_8x5_KHR;if(n===ia)return a===jt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_8x6_KHR:r.COMPRESSED_RGBA_ASTC_8x6_KHR;if(n===sa)return a===jt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_8x8_KHR:r.COMPRESSED_RGBA_ASTC_8x8_KHR;if(n===ra)return a===jt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_10x5_KHR:r.COMPRESSED_RGBA_ASTC_10x5_KHR;if(n===aa)return a===jt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_10x6_KHR:r.COMPRESSED_RGBA_ASTC_10x6_KHR;if(n===oa)return a===jt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_10x8_KHR:r.COMPRESSED_RGBA_ASTC_10x8_KHR;if(n===la)return a===jt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_10x10_KHR:r.COMPRESSED_RGBA_ASTC_10x10_KHR;if(n===ca)return a===jt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_12x10_KHR:r.COMPRESSED_RGBA_ASTC_12x10_KHR;if(n===ha)return a===jt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_12x12_KHR:r.COMPRESSED_RGBA_ASTC_12x12_KHR}else return null;if(n===ua||n===da||n===fa)if(r=t.get("EXT_texture_compression_bptc"),r!==null){if(n===ua)return a===jt?r.COMPRESSED_SRGB_ALPHA_BPTC_UNORM_EXT:r.COMPRESSED_RGBA_BPTC_UNORM_EXT;if(n===da)return r.COMPRESSED_RGB_BPTC_SIGNED_FLOAT_EXT;if(n===fa)return r.COMPRESSED_RGB_BPTC_UNSIGNED_FLOAT_EXT}else return null;if(n===pa||n===ma||n===ga||n===_a)if(r=t.get("EXT_texture_compression_rgtc"),r!==null){if(n===pa)return r.COMPRESSED_RED_RGTC1_EXT;if(n===ma)return r.COMPRESSED_SIGNED_RED_RGTC1_EXT;if(n===ga)return r.COMPRESSED_RED_GREEN_RGTC2_EXT;if(n===_a)return r.COMPRESSED_SIGNED_RED_GREEN_RGTC2_EXT}else return null;return n===Xi?s.UNSIGNED_INT_24_8:s[n]!==void 0?s[n]:null}return{convert:e}}const wm=`
void main() {

	gl_Position = vec4( position, 1.0 );

}`,Am=`
uniform sampler2DArray depthColor;
uniform float depthWidth;
uniform float depthHeight;

void main() {

	vec2 coord = vec2( gl_FragCoord.x / depthWidth, gl_FragCoord.y / depthHeight );

	if ( coord.x >= 1.0 ) {

		gl_FragDepth = texture( depthColor, vec3( coord.x - 1.0, coord.y, 1 ) ).r;

	} else {

		gl_FragDepth = texture( depthColor, vec3( coord.x, coord.y, 0 ) ).r;

	}

}`;class Cm{constructor(){this.texture=null,this.mesh=null,this.depthNear=0,this.depthFar=0}init(t,e){if(this.texture===null){const n=new Wl(t.texture);(t.depthNear!==e.depthNear||t.depthFar!==e.depthFar)&&(this.depthNear=t.depthNear,this.depthFar=t.depthFar),this.texture=n}}getMesh(t){if(this.texture!==null&&this.mesh===null){const e=t.cameras[0].viewport,n=new Ce({vertexShader:wm,fragmentShader:Am,uniforms:{depthColor:{value:this.texture},depthWidth:{value:e.z},depthHeight:{value:e.w}}});this.mesh=new Me(new Ji(20,20),n)}return this.mesh}reset(){this.texture=null,this.mesh=null}getDepthTexture(){return this.texture}}class Rm extends Zn{constructor(t,e){super();const n=this;let i=null,r=1,a=null,o="local-floor",l=1,c=null,h=null,d=null,u=null,p=null,g=null;const v=typeof XRWebGLBinding<"u",m=new Cm,f={},E=e.getContextAttributes();let b=null,S=null;const w=[],A=[],C=new dt;let D=null;const y=new We;y.viewport=new Jt;const x=new We;x.viewport=new Jt;const L=[y,x],N=new Yh;let k=null,X=null;this.cameraAutoUpdate=!0,this.enabled=!1,this.isPresenting=!1,this.getController=function(Y){let $=w[Y];return $===void 0&&($=new pr,w[Y]=$),$.getTargetRaySpace()},this.getControllerGrip=function(Y){let $=w[Y];return $===void 0&&($=new pr,w[Y]=$),$.getGripSpace()},this.getHand=function(Y){let $=w[Y];return $===void 0&&($=new pr,w[Y]=$),$.getHandSpace()};function W(Y){const $=A.indexOf(Y.inputSource);if($===-1)return;const ft=w[$];ft!==void 0&&(ft.update(Y.inputSource,Y.frame,c||a),ft.dispatchEvent({type:Y.type,data:Y.inputSource}))}function q(){i.removeEventListener("select",W),i.removeEventListener("selectstart",W),i.removeEventListener("selectend",W),i.removeEventListener("squeeze",W),i.removeEventListener("squeezestart",W),i.removeEventListener("squeezeend",W),i.removeEventListener("end",q),i.removeEventListener("inputsourceschange",Z);for(let Y=0;Y<w.length;Y++){const $=A[Y];$!==null&&(A[Y]=null,w[Y].disconnect($))}k=null,X=null,m.reset();for(const Y in f)delete f[Y];t.setRenderTarget(b),p=null,u=null,d=null,i=null,S=null,qt.stop(),n.isPresenting=!1,t.setPixelRatio(D),t.setSize(C.width,C.height,!1),n.dispatchEvent({type:"sessionend"})}this.setFramebufferScaleFactor=function(Y){r=Y,n.isPresenting===!0&&console.warn("THREE.WebXRManager: Cannot change framebuffer scale while presenting.")},this.setReferenceSpaceType=function(Y){o=Y,n.isPresenting===!0&&console.warn("THREE.WebXRManager: Cannot change reference space type while presenting.")},this.getReferenceSpace=function(){return c||a},this.setReferenceSpace=function(Y){c=Y},this.getBaseLayer=function(){return u!==null?u:p},this.getBinding=function(){return d===null&&v&&(d=new XRWebGLBinding(i,e)),d},this.getFrame=function(){return g},this.getSession=function(){return i},this.setSession=async function(Y){if(i=Y,i!==null){if(b=t.getRenderTarget(),i.addEventListener("select",W),i.addEventListener("selectstart",W),i.addEventListener("selectend",W),i.addEventListener("squeeze",W),i.addEventListener("squeezestart",W),i.addEventListener("squeezeend",W),i.addEventListener("end",q),i.addEventListener("inputsourceschange",Z),E.xrCompatible!==!0&&await e.makeXRCompatible(),D=t.getPixelRatio(),t.getSize(C),v&&"createProjectionLayer"in XRWebGLBinding.prototype){let ft=null,Pt=null,St=null;E.depth&&(St=E.stencil?e.DEPTH24_STENCIL8:e.DEPTH_COMPONENT24,ft=E.stencil?Yi:qi,Pt=E.stencil?Xi:jn);const Vt={colorFormat:e.RGBA8,depthFormat:St,scaleFactor:r};d=this.getBinding(),u=d.createProjectionLayer(Vt),i.updateRenderState({layers:[u]}),t.setPixelRatio(1),t.setSize(u.textureWidth,u.textureHeight,!1),S=new $e(u.textureWidth,u.textureHeight,{format:Ze,type:ln,depthTexture:new Gl(u.textureWidth,u.textureHeight,Pt,void 0,void 0,void 0,void 0,void 0,void 0,ft),stencilBuffer:E.stencil,colorSpace:t.outputColorSpace,samples:E.antialias?4:0,resolveDepthBuffer:u.ignoreDepthValues===!1,resolveStencilBuffer:u.ignoreDepthValues===!1})}else{const ft={antialias:E.antialias,alpha:!0,depth:E.depth,stencil:E.stencil,framebufferScaleFactor:r};p=new XRWebGLLayer(i,e,ft),i.updateRenderState({baseLayer:p}),t.setPixelRatio(1),t.setSize(p.framebufferWidth,p.framebufferHeight,!1),S=new $e(p.framebufferWidth,p.framebufferHeight,{format:Ze,type:ln,colorSpace:t.outputColorSpace,stencilBuffer:E.stencil,resolveDepthBuffer:p.ignoreDepthValues===!1,resolveStencilBuffer:p.ignoreDepthValues===!1})}S.isXRRenderTarget=!0,this.setFoveation(l),c=null,a=await i.requestReferenceSpace(o),qt.setContext(i),qt.start(),n.isPresenting=!0,n.dispatchEvent({type:"sessionstart"})}},this.getEnvironmentBlendMode=function(){if(i!==null)return i.environmentBlendMode},this.getDepthTexture=function(){return m.getDepthTexture()};function Z(Y){for(let $=0;$<Y.removed.length;$++){const ft=Y.removed[$],Pt=A.indexOf(ft);Pt>=0&&(A[Pt]=null,w[Pt].disconnect(ft))}for(let $=0;$<Y.added.length;$++){const ft=Y.added[$];let Pt=A.indexOf(ft);if(Pt===-1){for(let Vt=0;Vt<w.length;Vt++)if(Vt>=A.length){A.push(ft),Pt=Vt;break}else if(A[Vt]===null){A[Vt]=ft,Pt=Vt;break}if(Pt===-1)break}const St=w[Pt];St&&St.connect(ft)}}const H=new P,rt=new P;function ct(Y,$,ft){H.setFromMatrixPosition($.matrixWorld),rt.setFromMatrixPosition(ft.matrixWorld);const Pt=H.distanceTo(rt),St=$.projectionMatrix.elements,Vt=ft.projectionMatrix.elements,Se=St[14]/(St[10]-1),R=St[14]/(St[10]+1),re=(St[9]+1)/St[5],It=(St[9]-1)/St[5],Ct=(St[8]-1)/St[0],gt=(Vt[8]+1)/Vt[0],ae=Se*Ct,_t=Se*gt,Ot=Pt/(-Ct+gt),xe=Ot*-Ct;if($.matrixWorld.decompose(Y.position,Y.quaternion,Y.scale),Y.translateX(xe),Y.translateZ(Ot),Y.matrixWorld.compose(Y.position,Y.quaternion,Y.scale),Y.matrixWorldInverse.copy(Y.matrixWorld).invert(),St[10]===-1)Y.projectionMatrix.copy($.projectionMatrix),Y.projectionMatrixInverse.copy($.projectionMatrixInverse);else{const he=Se+Ot,T=R+Ot,_=ae-xe,O=_t+(Pt-xe),G=re*R/T*he,K=It*R/T*he;Y.projectionMatrix.makePerspective(_,O,G,K,he,T),Y.projectionMatrixInverse.copy(Y.projectionMatrix).invert()}}function Et(Y,$){$===null?Y.matrixWorld.copy(Y.matrix):Y.matrixWorld.multiplyMatrices($.matrixWorld,Y.matrix),Y.matrixWorldInverse.copy(Y.matrixWorld).invert()}this.updateCamera=function(Y){if(i===null)return;let $=Y.near,ft=Y.far;m.texture!==null&&(m.depthNear>0&&($=m.depthNear),m.depthFar>0&&(ft=m.depthFar)),N.near=x.near=y.near=$,N.far=x.far=y.far=ft,(k!==N.near||X!==N.far)&&(i.updateRenderState({depthNear:N.near,depthFar:N.far}),k=N.near,X=N.far),N.layers.mask=Y.layers.mask|6,y.layers.mask=N.layers.mask&3,x.layers.mask=N.layers.mask&5;const Pt=Y.parent,St=N.cameras;Et(N,Pt);for(let Vt=0;Vt<St.length;Vt++)Et(St[Vt],Pt);St.length===2?ct(N,y,x):N.projectionMatrix.copy(y.projectionMatrix),kt(Y,N,Pt)};function kt(Y,$,ft){ft===null?Y.matrix.copy($.matrixWorld):(Y.matrix.copy(ft.matrixWorld),Y.matrix.invert(),Y.matrix.multiply($.matrixWorld)),Y.matrix.decompose(Y.position,Y.quaternion,Y.scale),Y.updateMatrixWorld(!0),Y.projectionMatrix.copy($.projectionMatrix),Y.projectionMatrixInverse.copy($.projectionMatrixInverse),Y.isPerspectiveCamera&&(Y.fov=ji*2*Math.atan(1/Y.projectionMatrix.elements[5]),Y.zoom=1)}this.getCamera=function(){return N},this.getFoveation=function(){if(!(u===null&&p===null))return l},this.setFoveation=function(Y){l=Y,u!==null&&(u.fixedFoveation=Y),p!==null&&p.fixedFoveation!==void 0&&(p.fixedFoveation=Y)},this.hasDepthSensing=function(){return m.texture!==null},this.getDepthSensingMesh=function(){return m.getMesh(N)},this.getCameraTexture=function(Y){return f[Y]};let te=null;function se(Y,$){if(h=$.getViewerPose(c||a),g=$,h!==null){const ft=h.views;p!==null&&(t.setRenderTargetFramebuffer(S,p.framebuffer),t.setRenderTarget(S));let Pt=!1;ft.length!==N.cameras.length&&(N.cameras.length=0,Pt=!0);for(let R=0;R<ft.length;R++){const re=ft[R];let It=null;if(p!==null)It=p.getViewport(re);else{const gt=d.getViewSubImage(u,re);It=gt.viewport,R===0&&(t.setRenderTargetTextures(S,gt.colorTexture,gt.depthStencilTexture),t.setRenderTarget(S))}let Ct=L[R];Ct===void 0&&(Ct=new We,Ct.layers.enable(R),Ct.viewport=new Jt,L[R]=Ct),Ct.matrix.fromArray(re.transform.matrix),Ct.matrix.decompose(Ct.position,Ct.quaternion,Ct.scale),Ct.projectionMatrix.fromArray(re.projectionMatrix),Ct.projectionMatrixInverse.copy(Ct.projectionMatrix).invert(),Ct.viewport.set(It.x,It.y,It.width,It.height),R===0&&(N.matrix.copy(Ct.matrix),N.matrix.decompose(N.position,N.quaternion,N.scale)),Pt===!0&&N.cameras.push(Ct)}const St=i.enabledFeatures;if(St&&St.includes("depth-sensing")&&i.depthUsage=="gpu-optimized"&&v){d=n.getBinding();const R=d.getDepthInformation(ft[0]);R&&R.isValid&&R.texture&&m.init(R,i.renderState)}if(St&&St.includes("camera-access")&&v){t.state.unbindTexture(),d=n.getBinding();for(let R=0;R<ft.length;R++){const re=ft[R].camera;if(re){let It=f[re];It||(It=new Wl,f[re]=It);const Ct=d.getCameraImage(re);It.sourceTexture=Ct}}}}for(let ft=0;ft<w.length;ft++){const Pt=A[ft],St=w[ft];Pt!==null&&St!==void 0&&St.update(Pt,$,c||a)}te&&te(Y,$),$.detectedPlanes&&n.dispatchEvent({type:"planesdetected",data:$}),g=null}const qt=new Yl;qt.setAnimationLoop(se),this.setAnimationLoop=function(Y){te=Y},this.dispose=function(){}}}const kn=new cn,Pm=new Kt;function Lm(s,t){function e(m,f){m.matrixAutoUpdate===!0&&m.updateMatrix(),f.value.copy(m.matrix)}function n(m,f){f.color.getRGB(m.fogColor.value,Bl(s)),f.isFog?(m.fogNear.value=f.near,m.fogFar.value=f.far):f.isFogExp2&&(m.fogDensity.value=f.density)}function i(m,f,E,b,S){f.isMeshBasicMaterial||f.isMeshLambertMaterial?r(m,f):f.isMeshToonMaterial?(r(m,f),d(m,f)):f.isMeshPhongMaterial?(r(m,f),h(m,f)):f.isMeshStandardMaterial?(r(m,f),u(m,f),f.isMeshPhysicalMaterial&&p(m,f,S)):f.isMeshMatcapMaterial?(r(m,f),g(m,f)):f.isMeshDepthMaterial?r(m,f):f.isMeshDistanceMaterial?(r(m,f),v(m,f)):f.isMeshNormalMaterial?r(m,f):f.isLineBasicMaterial?(a(m,f),f.isLineDashedMaterial&&o(m,f)):f.isPointsMaterial?l(m,f,E,b):f.isSpriteMaterial?c(m,f):f.isShadowMaterial?(m.color.value.copy(f.color),m.opacity.value=f.opacity):f.isShaderMaterial&&(f.uniformsNeedUpdate=!1)}function r(m,f){m.opacity.value=f.opacity,f.color&&m.diffuse.value.copy(f.color),f.emissive&&m.emissive.value.copy(f.emissive).multiplyScalar(f.emissiveIntensity),f.map&&(m.map.value=f.map,e(f.map,m.mapTransform)),f.alphaMap&&(m.alphaMap.value=f.alphaMap,e(f.alphaMap,m.alphaMapTransform)),f.bumpMap&&(m.bumpMap.value=f.bumpMap,e(f.bumpMap,m.bumpMapTransform),m.bumpScale.value=f.bumpScale,f.side===Ie&&(m.bumpScale.value*=-1)),f.normalMap&&(m.normalMap.value=f.normalMap,e(f.normalMap,m.normalMapTransform),m.normalScale.value.copy(f.normalScale),f.side===Ie&&m.normalScale.value.negate()),f.displacementMap&&(m.displacementMap.value=f.displacementMap,e(f.displacementMap,m.displacementMapTransform),m.displacementScale.value=f.displacementScale,m.displacementBias.value=f.displacementBias),f.emissiveMap&&(m.emissiveMap.value=f.emissiveMap,e(f.emissiveMap,m.emissiveMapTransform)),f.specularMap&&(m.specularMap.value=f.specularMap,e(f.specularMap,m.specularMapTransform)),f.alphaTest>0&&(m.alphaTest.value=f.alphaTest);const E=t.get(f),b=E.envMap,S=E.envMapRotation;b&&(m.envMap.value=b,kn.copy(S),kn.x*=-1,kn.y*=-1,kn.z*=-1,b.isCubeTexture&&b.isRenderTargetTexture===!1&&(kn.y*=-1,kn.z*=-1),m.envMapRotation.value.setFromMatrix4(Pm.makeRotationFromEuler(kn)),m.flipEnvMap.value=b.isCubeTexture&&b.isRenderTargetTexture===!1?-1:1,m.reflectivity.value=f.reflectivity,m.ior.value=f.ior,m.refractionRatio.value=f.refractionRatio),f.lightMap&&(m.lightMap.value=f.lightMap,m.lightMapIntensity.value=f.lightMapIntensity,e(f.lightMap,m.lightMapTransform)),f.aoMap&&(m.aoMap.value=f.aoMap,m.aoMapIntensity.value=f.aoMapIntensity,e(f.aoMap,m.aoMapTransform))}function a(m,f){m.diffuse.value.copy(f.color),m.opacity.value=f.opacity,f.map&&(m.map.value=f.map,e(f.map,m.mapTransform))}function o(m,f){m.dashSize.value=f.dashSize,m.totalSize.value=f.dashSize+f.gapSize,m.scale.value=f.scale}function l(m,f,E,b){m.diffuse.value.copy(f.color),m.opacity.value=f.opacity,m.size.value=f.size*E,m.scale.value=b*.5,f.map&&(m.map.value=f.map,e(f.map,m.uvTransform)),f.alphaMap&&(m.alphaMap.value=f.alphaMap,e(f.alphaMap,m.alphaMapTransform)),f.alphaTest>0&&(m.alphaTest.value=f.alphaTest)}function c(m,f){m.diffuse.value.copy(f.color),m.opacity.value=f.opacity,m.rotation.value=f.rotation,f.map&&(m.map.value=f.map,e(f.map,m.mapTransform)),f.alphaMap&&(m.alphaMap.value=f.alphaMap,e(f.alphaMap,m.alphaMapTransform)),f.alphaTest>0&&(m.alphaTest.value=f.alphaTest)}function h(m,f){m.specular.value.copy(f.specular),m.shininess.value=Math.max(f.shininess,1e-4)}function d(m,f){f.gradientMap&&(m.gradientMap.value=f.gradientMap)}function u(m,f){m.metalness.value=f.metalness,f.metalnessMap&&(m.metalnessMap.value=f.metalnessMap,e(f.metalnessMap,m.metalnessMapTransform)),m.roughness.value=f.roughness,f.roughnessMap&&(m.roughnessMap.value=f.roughnessMap,e(f.roughnessMap,m.roughnessMapTransform)),f.envMap&&(m.envMapIntensity.value=f.envMapIntensity)}function p(m,f,E){m.ior.value=f.ior,f.sheen>0&&(m.sheenColor.value.copy(f.sheenColor).multiplyScalar(f.sheen),m.sheenRoughness.value=f.sheenRoughness,f.sheenColorMap&&(m.sheenColorMap.value=f.sheenColorMap,e(f.sheenColorMap,m.sheenColorMapTransform)),f.sheenRoughnessMap&&(m.sheenRoughnessMap.value=f.sheenRoughnessMap,e(f.sheenRoughnessMap,m.sheenRoughnessMapTransform))),f.clearcoat>0&&(m.clearcoat.value=f.clearcoat,m.clearcoatRoughness.value=f.clearcoatRoughness,f.clearcoatMap&&(m.clearcoatMap.value=f.clearcoatMap,e(f.clearcoatMap,m.clearcoatMapTransform)),f.clearcoatRoughnessMap&&(m.clearcoatRoughnessMap.value=f.clearcoatRoughnessMap,e(f.clearcoatRoughnessMap,m.clearcoatRoughnessMapTransform)),f.clearcoatNormalMap&&(m.clearcoatNormalMap.value=f.clearcoatNormalMap,e(f.clearcoatNormalMap,m.clearcoatNormalMapTransform),m.clearcoatNormalScale.value.copy(f.clearcoatNormalScale),f.side===Ie&&m.clearcoatNormalScale.value.negate())),f.dispersion>0&&(m.dispersion.value=f.dispersion),f.iridescence>0&&(m.iridescence.value=f.iridescence,m.iridescenceIOR.value=f.iridescenceIOR,m.iridescenceThicknessMinimum.value=f.iridescenceThicknessRange[0],m.iridescenceThicknessMaximum.value=f.iridescenceThicknessRange[1],f.iridescenceMap&&(m.iridescenceMap.value=f.iridescenceMap,e(f.iridescenceMap,m.iridescenceMapTransform)),f.iridescenceThicknessMap&&(m.iridescenceThicknessMap.value=f.iridescenceThicknessMap,e(f.iridescenceThicknessMap,m.iridescenceThicknessMapTransform))),f.transmission>0&&(m.transmission.value=f.transmission,m.transmissionSamplerMap.value=E.texture,m.transmissionSamplerSize.value.set(E.width,E.height),f.transmissionMap&&(m.transmissionMap.value=f.transmissionMap,e(f.transmissionMap,m.transmissionMapTransform)),m.thickness.value=f.thickness,f.thicknessMap&&(m.thicknessMap.value=f.thicknessMap,e(f.thicknessMap,m.thicknessMapTransform)),m.attenuationDistance.value=f.attenuationDistance,m.attenuationColor.value.copy(f.attenuationColor)),f.anisotropy>0&&(m.anisotropyVector.value.set(f.anisotropy*Math.cos(f.anisotropyRotation),f.anisotropy*Math.sin(f.anisotropyRotation)),f.anisotropyMap&&(m.anisotropyMap.value=f.anisotropyMap,e(f.anisotropyMap,m.anisotropyMapTransform))),m.specularIntensity.value=f.specularIntensity,m.specularColor.value.copy(f.specularColor),f.specularColorMap&&(m.specularColorMap.value=f.specularColorMap,e(f.specularColorMap,m.specularColorMapTransform)),f.specularIntensityMap&&(m.specularIntensityMap.value=f.specularIntensityMap,e(f.specularIntensityMap,m.specularIntensityMapTransform))}function g(m,f){f.matcap&&(m.matcap.value=f.matcap)}function v(m,f){const E=t.get(f).light;m.referencePosition.value.setFromMatrixPosition(E.matrixWorld),m.nearDistance.value=E.shadow.camera.near,m.farDistance.value=E.shadow.camera.far}return{refreshFogUniforms:n,refreshMaterialUniforms:i}}function Dm(s,t,e,n){let i={},r={},a=[];const o=s.getParameter(s.MAX_UNIFORM_BUFFER_BINDINGS);function l(E,b){const S=b.program;n.uniformBlockBinding(E,S)}function c(E,b){let S=i[E.id];S===void 0&&(g(E),S=h(E),i[E.id]=S,E.addEventListener("dispose",m));const w=b.program;n.updateUBOMapping(E,w);const A=t.render.frame;r[E.id]!==A&&(u(E),r[E.id]=A)}function h(E){const b=d();E.__bindingPointIndex=b;const S=s.createBuffer(),w=E.__size,A=E.usage;return s.bindBuffer(s.UNIFORM_BUFFER,S),s.bufferData(s.UNIFORM_BUFFER,w,A),s.bindBuffer(s.UNIFORM_BUFFER,null),s.bindBufferBase(s.UNIFORM_BUFFER,b,S),S}function d(){for(let E=0;E<o;E++)if(a.indexOf(E)===-1)return a.push(E),E;return console.error("THREE.WebGLRenderer: Maximum number of simultaneously usable uniforms groups reached."),0}function u(E){const b=i[E.id],S=E.uniforms,w=E.__cache;s.bindBuffer(s.UNIFORM_BUFFER,b);for(let A=0,C=S.length;A<C;A++){const D=Array.isArray(S[A])?S[A]:[S[A]];for(let y=0,x=D.length;y<x;y++){const L=D[y];if(p(L,A,y,w)===!0){const N=L.__offset,k=Array.isArray(L.value)?L.value:[L.value];let X=0;for(let W=0;W<k.length;W++){const q=k[W],Z=v(q);typeof q=="number"||typeof q=="boolean"?(L.__data[0]=q,s.bufferSubData(s.UNIFORM_BUFFER,N+X,L.__data)):q.isMatrix3?(L.__data[0]=q.elements[0],L.__data[1]=q.elements[1],L.__data[2]=q.elements[2],L.__data[3]=0,L.__data[4]=q.elements[3],L.__data[5]=q.elements[4],L.__data[6]=q.elements[5],L.__data[7]=0,L.__data[8]=q.elements[6],L.__data[9]=q.elements[7],L.__data[10]=q.elements[8],L.__data[11]=0):(q.toArray(L.__data,X),X+=Z.storage/Float32Array.BYTES_PER_ELEMENT)}s.bufferSubData(s.UNIFORM_BUFFER,N,L.__data)}}}s.bindBuffer(s.UNIFORM_BUFFER,null)}function p(E,b,S,w){const A=E.value,C=b+"_"+S;if(w[C]===void 0)return typeof A=="number"||typeof A=="boolean"?w[C]=A:w[C]=A.clone(),!0;{const D=w[C];if(typeof A=="number"||typeof A=="boolean"){if(D!==A)return w[C]=A,!0}else if(D.equals(A)===!1)return D.copy(A),!0}return!1}function g(E){const b=E.uniforms;let S=0;const w=16;for(let C=0,D=b.length;C<D;C++){const y=Array.isArray(b[C])?b[C]:[b[C]];for(let x=0,L=y.length;x<L;x++){const N=y[x],k=Array.isArray(N.value)?N.value:[N.value];for(let X=0,W=k.length;X<W;X++){const q=k[X],Z=v(q),H=S%w,rt=H%Z.boundary,ct=H+rt;S+=rt,ct!==0&&w-ct<Z.storage&&(S+=w-ct),N.__data=new Float32Array(Z.storage/Float32Array.BYTES_PER_ELEMENT),N.__offset=S,S+=Z.storage}}}const A=S%w;return A>0&&(S+=w-A),E.__size=S,E.__cache={},this}function v(E){const b={boundary:0,storage:0};return typeof E=="number"||typeof E=="boolean"?(b.boundary=4,b.storage=4):E.isVector2?(b.boundary=8,b.storage=8):E.isVector3||E.isColor?(b.boundary=16,b.storage=12):E.isVector4?(b.boundary=16,b.storage=16):E.isMatrix3?(b.boundary=48,b.storage=48):E.isMatrix4?(b.boundary=64,b.storage=64):E.isTexture?console.warn("THREE.WebGLRenderer: Texture samplers can not be part of an uniforms group."):console.warn("THREE.WebGLRenderer: Unsupported uniform value type.",E),b}function m(E){const b=E.target;b.removeEventListener("dispose",m);const S=a.indexOf(b.__bindingPointIndex);a.splice(S,1),s.deleteBuffer(i[b.id]),delete i[b.id],delete r[b.id]}function f(){for(const E in i)s.deleteBuffer(i[E]);a=[],i={},r={}}return{bind:l,update:c,dispose:f}}class Im{constructor(t={}){const{canvas:e=eh(),context:n=null,depth:i=!0,stencil:r=!1,alpha:a=!1,antialias:o=!1,premultipliedAlpha:l=!0,preserveDrawingBuffer:c=!1,powerPreference:h="default",failIfMajorPerformanceCaveat:d=!1,reversedDepthBuffer:u=!1}=t;this.isWebGLRenderer=!0;let p;if(n!==null){if(typeof WebGLRenderingContext<"u"&&n instanceof WebGLRenderingContext)throw new Error("THREE.WebGLRenderer: WebGL 1 is not supported since r163.");p=n.getContextAttributes().alpha}else p=a;const g=new Uint32Array(4),v=new Int32Array(4);let m=null,f=null;const E=[],b=[];this.domElement=e,this.debug={checkShaderErrors:!0,onShaderError:null},this.autoClear=!0,this.autoClearColor=!0,this.autoClearDepth=!0,this.autoClearStencil=!0,this.sortObjects=!0,this.clippingPlanes=[],this.localClippingEnabled=!1,this.toneMapping=Ln,this.toneMappingExposure=1,this.transmissionResolutionScale=1;const S=this;let w=!1;this._outputColorSpace=Be;let A=0,C=0,D=null,y=-1,x=null;const L=new Jt,N=new Jt;let k=null;const X=new Ft(0);let W=0,q=e.width,Z=e.height,H=1,rt=null,ct=null;const Et=new Jt(0,0,q,Z),kt=new Jt(0,0,q,Z);let te=!1;const se=new Ua;let qt=!1,Y=!1;const $=new Kt,ft=new P,Pt=new Jt,St={background:null,fog:null,environment:null,overrideMaterial:null,isScene:!0};let Vt=!1;function Se(){return D===null?H:1}let R=n;function re(M,U){return e.getContext(M,U)}try{const M={alpha:!0,depth:i,stencil:r,antialias:o,premultipliedAlpha:l,preserveDrawingBuffer:c,powerPreference:h,failIfMajorPerformanceCaveat:d};if("setAttribute"in e&&e.setAttribute("data-engine",`three.js r${Ma}`),e.addEventListener("webglcontextlost",it,!1),e.addEventListener("webglcontextrestored",ut,!1),e.addEventListener("webglcontextcreationerror",J,!1),R===null){const U="webgl2";if(R=re(U,M),R===null)throw re(U)?new Error("Error creating WebGL context with your selected attributes."):new Error("Error creating WebGL context.")}}catch(M){throw console.error("THREE.WebGLRenderer: "+M.message),M}let It,Ct,gt,ae,_t,Ot,xe,he,T,_,O,G,K,V,Mt,nt,xt,vt,tt,lt,At,yt,at,Ut;function I(){It=new Gf(R),It.init(),yt=new Tm(R,It),Ct=new Ff(R,It,t,yt),gt=new Em(R,It),Ct.reversedDepthBuffer&&u&&gt.buffers.depth.setReversed(!0),ae=new qf(R),_t=new hm,Ot=new bm(R,It,gt,_t,Ct,yt,ae),xe=new Bf(S),he=new Vf(S),T=new $h(R),at=new Uf(R,T),_=new Wf(R,T,ae,at),O=new jf(R,_,T,ae),tt=new Yf(R,Ct,Ot),nt=new Of(_t),G=new cm(S,xe,he,It,Ct,at,nt),K=new Lm(S,_t),V=new dm,Mt=new xm(It),vt=new If(S,xe,he,gt,O,p,l),xt=new Mm(S,O,Ct),Ut=new Dm(R,ae,Ct,gt),lt=new Nf(R,It,ae),At=new Xf(R,It,ae),ae.programs=G.programs,S.capabilities=Ct,S.extensions=It,S.properties=_t,S.renderLists=V,S.shadowMap=xt,S.state=gt,S.info=ae}I();const et=new Rm(S,R);this.xr=et,this.getContext=function(){return R},this.getContextAttributes=function(){return R.getContextAttributes()},this.forceContextLoss=function(){const M=It.get("WEBGL_lose_context");M&&M.loseContext()},this.forceContextRestore=function(){const M=It.get("WEBGL_lose_context");M&&M.restoreContext()},this.getPixelRatio=function(){return H},this.setPixelRatio=function(M){M!==void 0&&(H=M,this.setSize(q,Z,!1))},this.getSize=function(M){return M.set(q,Z)},this.setSize=function(M,U,B=!0){if(et.isPresenting){console.warn("THREE.WebGLRenderer: Can't change size while VR device is presenting.");return}q=M,Z=U,e.width=Math.floor(M*H),e.height=Math.floor(U*H),B===!0&&(e.style.width=M+"px",e.style.height=U+"px"),this.setViewport(0,0,M,U)},this.getDrawingBufferSize=function(M){return M.set(q*H,Z*H).floor()},this.setDrawingBufferSize=function(M,U,B){q=M,Z=U,H=B,e.width=Math.floor(M*B),e.height=Math.floor(U*B),this.setViewport(0,0,M,U)},this.getCurrentViewport=function(M){return M.copy(L)},this.getViewport=function(M){return M.copy(Et)},this.setViewport=function(M,U,B,z){M.isVector4?Et.set(M.x,M.y,M.z,M.w):Et.set(M,U,B,z),gt.viewport(L.copy(Et).multiplyScalar(H).round())},this.getScissor=function(M){return M.copy(kt)},this.setScissor=function(M,U,B,z){M.isVector4?kt.set(M.x,M.y,M.z,M.w):kt.set(M,U,B,z),gt.scissor(N.copy(kt).multiplyScalar(H).round())},this.getScissorTest=function(){return te},this.setScissorTest=function(M){gt.setScissorTest(te=M)},this.setOpaqueSort=function(M){rt=M},this.setTransparentSort=function(M){ct=M},this.getClearColor=function(M){return M.copy(vt.getClearColor())},this.setClearColor=function(){vt.setClearColor(...arguments)},this.getClearAlpha=function(){return vt.getClearAlpha()},this.setClearAlpha=function(){vt.setClearAlpha(...arguments)},this.clear=function(M=!0,U=!0,B=!0){let z=0;if(M){let F=!1;if(D!==null){const Q=D.texture.format;F=Q===Ra||Q===Ca||Q===Aa}if(F){const Q=D.texture.type,ot=Q===ln||Q===jn||Q===Wi||Q===Xi||Q===ba||Q===Ta,pt=vt.getClearColor(),ht=vt.getClearAlpha(),wt=pt.r,Rt=pt.g,bt=pt.b;ot?(g[0]=wt,g[1]=Rt,g[2]=bt,g[3]=ht,R.clearBufferuiv(R.COLOR,0,g)):(v[0]=wt,v[1]=Rt,v[2]=bt,v[3]=ht,R.clearBufferiv(R.COLOR,0,v))}else z|=R.COLOR_BUFFER_BIT}U&&(z|=R.DEPTH_BUFFER_BIT),B&&(z|=R.STENCIL_BUFFER_BIT,this.state.buffers.stencil.setMask(4294967295)),R.clear(z)},this.clearColor=function(){this.clear(!0,!1,!1)},this.clearDepth=function(){this.clear(!1,!0,!1)},this.clearStencil=function(){this.clear(!1,!1,!0)},this.dispose=function(){e.removeEventListener("webglcontextlost",it,!1),e.removeEventListener("webglcontextrestored",ut,!1),e.removeEventListener("webglcontextcreationerror",J,!1),vt.dispose(),V.dispose(),Mt.dispose(),_t.dispose(),xe.dispose(),he.dispose(),O.dispose(),at.dispose(),Ut.dispose(),G.dispose(),et.dispose(),et.removeEventListener("sessionstart",Je),et.removeEventListener("sessionend",Wa),In.stop()};function it(M){M.preventDefault(),console.log("THREE.WebGLRenderer: Context Lost."),w=!0}function ut(){console.log("THREE.WebGLRenderer: Context Restored."),w=!1;const M=ae.autoReset,U=xt.enabled,B=xt.autoUpdate,z=xt.needsUpdate,F=xt.type;I(),ae.autoReset=M,xt.enabled=U,xt.autoUpdate=B,xt.needsUpdate=z,xt.type=F}function J(M){console.error("THREE.WebGLRenderer: A WebGL context could not be created. Reason: ",M.statusMessage)}function j(M){const U=M.target;U.removeEventListener("dispose",j),mt(U)}function mt(M){Lt(M),_t.remove(M)}function Lt(M){const U=_t.get(M).programs;U!==void 0&&(U.forEach(function(B){G.releaseProgram(B)}),M.isShaderMaterial&&G.releaseShaderCache(M))}this.renderBufferDirect=function(M,U,B,z,F,Q){U===null&&(U=St);const ot=F.isMesh&&F.matrixWorld.determinant()<0,pt=tc(M,U,B,z,F);gt.setMaterial(z,ot);let ht=B.index,wt=1;if(z.wireframe===!0){if(ht=_.getWireframeAttribute(B),ht===void 0)return;wt=2}const Rt=B.drawRange,bt=B.attributes.position;let Ht=Rt.start*wt,Zt=(Rt.start+Rt.count)*wt;Q!==null&&(Ht=Math.max(Ht,Q.start*wt),Zt=Math.min(Zt,(Q.start+Q.count)*wt)),ht!==null?(Ht=Math.max(Ht,0),Zt=Math.min(Zt,ht.count)):bt!=null&&(Ht=Math.max(Ht,0),Zt=Math.min(Zt,bt.count));const ce=Zt-Ht;if(ce<0||ce===1/0)return;at.setup(F,z,pt,B,ht);let ie,Qt=lt;if(ht!==null&&(ie=T.get(ht),Qt=At,Qt.setIndex(ie)),F.isMesh)z.wireframe===!0?(gt.setLineWidth(z.wireframeLinewidth*Se()),Qt.setMode(R.LINES)):Qt.setMode(R.TRIANGLES);else if(F.isLine){let Tt=z.linewidth;Tt===void 0&&(Tt=1),gt.setLineWidth(Tt*Se()),F.isLineSegments?Qt.setMode(R.LINES):F.isLineLoop?Qt.setMode(R.LINE_LOOP):Qt.setMode(R.LINE_STRIP)}else F.isPoints?Qt.setMode(R.POINTS):F.isSprite&&Qt.setMode(R.TRIANGLES);if(F.isBatchedMesh)if(F._multiDrawInstances!==null)Ki("THREE.WebGLRenderer: renderMultiDrawInstances has been deprecated and will be removed in r184. Append to renderMultiDraw arguments and use indirection."),Qt.renderMultiDrawInstances(F._multiDrawStarts,F._multiDrawCounts,F._multiDrawCount,F._multiDrawInstances);else if(It.get("WEBGL_multi_draw"))Qt.renderMultiDraw(F._multiDrawStarts,F._multiDrawCounts,F._multiDrawCount);else{const Tt=F._multiDrawStarts,oe=F._multiDrawCounts,Wt=F._multiDrawCount,Ue=ht?T.get(ht).bytesPerElement:1,ti=_t.get(z).currentProgram.getUniforms();for(let Ne=0;Ne<Wt;Ne++)ti.setValue(R,"_gl_DrawID",Ne),Qt.render(Tt[Ne]/Ue,oe[Ne])}else if(F.isInstancedMesh)Qt.renderInstances(Ht,ce,F.count);else if(B.isInstancedBufferGeometry){const Tt=B._maxInstanceCount!==void 0?B._maxInstanceCount:1/0,oe=Math.min(B.instanceCount,Tt);Qt.renderInstances(Ht,ce,oe)}else Qt.render(Ht,ce)};function ee(M,U,B){M.transparent===!0&&M.side===sn&&M.forceSinglePass===!1?(M.side=Ie,M.needsUpdate=!0,ts(M,U,B),M.side=Dn,M.needsUpdate=!0,ts(M,U,B),M.side=sn):ts(M,U,B)}this.compile=function(M,U,B=null){B===null&&(B=M),f=Mt.get(B),f.init(U),b.push(f),B.traverseVisible(function(F){F.isLight&&F.layers.test(U.layers)&&(f.pushLight(F),F.castShadow&&f.pushShadow(F))}),M!==B&&M.traverseVisible(function(F){F.isLight&&F.layers.test(U.layers)&&(f.pushLight(F),F.castShadow&&f.pushShadow(F))}),f.setupLights();const z=new Set;return M.traverse(function(F){if(!(F.isMesh||F.isPoints||F.isLine||F.isSprite))return;const Q=F.material;if(Q)if(Array.isArray(Q))for(let ot=0;ot<Q.length;ot++){const pt=Q[ot];ee(pt,B,F),z.add(pt)}else ee(Q,B,F),z.add(Q)}),f=b.pop(),z},this.compileAsync=function(M,U,B=null){const z=this.compile(M,U,B);return new Promise(F=>{function Q(){if(z.forEach(function(ot){_t.get(ot).currentProgram.isReady()&&z.delete(ot)}),z.size===0){F(M);return}setTimeout(Q,10)}It.get("KHR_parallel_shader_compile")!==null?Q():setTimeout(Q,10)})};let Yt=null;function hn(M){Yt&&Yt(M)}function Je(){In.stop()}function Wa(){In.start()}const In=new Yl;In.setAnimationLoop(hn),typeof self<"u"&&In.setContext(self),this.setAnimationLoop=function(M){Yt=M,et.setAnimationLoop(M),M===null?In.stop():In.start()},et.addEventListener("sessionstart",Je),et.addEventListener("sessionend",Wa),this.render=function(M,U){if(U!==void 0&&U.isCamera!==!0){console.error("THREE.WebGLRenderer.render: camera is not an instance of THREE.Camera.");return}if(w===!0)return;if(M.matrixWorldAutoUpdate===!0&&M.updateMatrixWorld(),U.parent===null&&U.matrixWorldAutoUpdate===!0&&U.updateMatrixWorld(),et.enabled===!0&&et.isPresenting===!0&&(et.cameraAutoUpdate===!0&&et.updateCamera(U),U=et.getCamera()),M.isScene===!0&&M.onBeforeRender(S,M,U,D),f=Mt.get(M,b.length),f.init(U),b.push(f),$.multiplyMatrices(U.projectionMatrix,U.matrixWorldInverse),se.setFromProjectionMatrix($,on,U.reversedDepth),Y=this.localClippingEnabled,qt=nt.init(this.clippingPlanes,Y),m=V.get(M,E.length),m.init(),E.push(m),et.enabled===!0&&et.isPresenting===!0){const Q=S.xr.getDepthSensingMesh();Q!==null&&Xs(Q,U,-1/0,S.sortObjects)}Xs(M,U,0,S.sortObjects),m.finish(),S.sortObjects===!0&&m.sort(rt,ct),Vt=et.enabled===!1||et.isPresenting===!1||et.hasDepthSensing()===!1,Vt&&vt.addToRenderList(m,M),this.info.render.frame++,qt===!0&&nt.beginShadows();const B=f.state.shadowsArray;xt.render(B,M,U),qt===!0&&nt.endShadows(),this.info.autoReset===!0&&this.info.reset();const z=m.opaque,F=m.transmissive;if(f.setupLights(),U.isArrayCamera){const Q=U.cameras;if(F.length>0)for(let ot=0,pt=Q.length;ot<pt;ot++){const ht=Q[ot];qa(z,F,M,ht)}Vt&&vt.render(M);for(let ot=0,pt=Q.length;ot<pt;ot++){const ht=Q[ot];Xa(m,M,ht,ht.viewport)}}else F.length>0&&qa(z,F,M,U),Vt&&vt.render(M),Xa(m,M,U);D!==null&&C===0&&(Ot.updateMultisampleRenderTarget(D),Ot.updateRenderTargetMipmap(D)),M.isScene===!0&&M.onAfterRender(S,M,U),at.resetDefaultState(),y=-1,x=null,b.pop(),b.length>0?(f=b[b.length-1],qt===!0&&nt.setGlobalState(S.clippingPlanes,f.state.camera)):f=null,E.pop(),E.length>0?m=E[E.length-1]:m=null};function Xs(M,U,B,z){if(M.visible===!1)return;if(M.layers.test(U.layers)){if(M.isGroup)B=M.renderOrder;else if(M.isLOD)M.autoUpdate===!0&&M.update(U);else if(M.isLight)f.pushLight(M),M.castShadow&&f.pushShadow(M);else if(M.isSprite){if(!M.frustumCulled||se.intersectsSprite(M)){z&&Pt.setFromMatrixPosition(M.matrixWorld).applyMatrix4($);const ot=O.update(M),pt=M.material;pt.visible&&m.push(M,ot,pt,B,Pt.z,null)}}else if((M.isMesh||M.isLine||M.isPoints)&&(!M.frustumCulled||se.intersectsObject(M))){const ot=O.update(M),pt=M.material;if(z&&(M.boundingSphere!==void 0?(M.boundingSphere===null&&M.computeBoundingSphere(),Pt.copy(M.boundingSphere.center)):(ot.boundingSphere===null&&ot.computeBoundingSphere(),Pt.copy(ot.boundingSphere.center)),Pt.applyMatrix4(M.matrixWorld).applyMatrix4($)),Array.isArray(pt)){const ht=ot.groups;for(let wt=0,Rt=ht.length;wt<Rt;wt++){const bt=ht[wt],Ht=pt[bt.materialIndex];Ht&&Ht.visible&&m.push(M,ot,Ht,B,Pt.z,bt)}}else pt.visible&&m.push(M,ot,pt,B,Pt.z,null)}}const Q=M.children;for(let ot=0,pt=Q.length;ot<pt;ot++)Xs(Q[ot],U,B,z)}function Xa(M,U,B,z){const F=M.opaque,Q=M.transmissive,ot=M.transparent;f.setupLightsView(B),qt===!0&&nt.setGlobalState(S.clippingPlanes,B),z&&gt.viewport(L.copy(z)),F.length>0&&Qi(F,U,B),Q.length>0&&Qi(Q,U,B),ot.length>0&&Qi(ot,U,B),gt.buffers.depth.setTest(!0),gt.buffers.depth.setMask(!0),gt.buffers.color.setMask(!0),gt.setPolygonOffset(!1)}function qa(M,U,B,z){if((B.isScene===!0?B.overrideMaterial:null)!==null)return;f.state.transmissionRenderTarget[z.id]===void 0&&(f.state.transmissionRenderTarget[z.id]=new $e(1,1,{generateMipmaps:!0,type:It.has("EXT_color_buffer_half_float")||It.has("EXT_color_buffer_float")?yn:ln,minFilter:Xn,samples:4,stencilBuffer:r,resolveDepthBuffer:!1,resolveStencilBuffer:!1,colorSpace:Gt.workingColorSpace}));const Q=f.state.transmissionRenderTarget[z.id],ot=z.viewport||L;Q.setSize(ot.z*S.transmissionResolutionScale,ot.w*S.transmissionResolutionScale);const pt=S.getRenderTarget(),ht=S.getActiveCubeFace(),wt=S.getActiveMipmapLevel();S.setRenderTarget(Q),S.getClearColor(X),W=S.getClearAlpha(),W<1&&S.setClearColor(16777215,.5),S.clear(),Vt&&vt.render(B);const Rt=S.toneMapping;S.toneMapping=Ln;const bt=z.viewport;if(z.viewport!==void 0&&(z.viewport=void 0),f.setupLightsView(z),qt===!0&&nt.setGlobalState(S.clippingPlanes,z),Qi(M,B,z),Ot.updateMultisampleRenderTarget(Q),Ot.updateRenderTargetMipmap(Q),It.has("WEBGL_multisampled_render_to_texture")===!1){let Ht=!1;for(let Zt=0,ce=U.length;Zt<ce;Zt++){const ie=U[Zt],Qt=ie.object,Tt=ie.geometry,oe=ie.material,Wt=ie.group;if(oe.side===sn&&Qt.layers.test(z.layers)){const Ue=oe.side;oe.side=Ie,oe.needsUpdate=!0,Ya(Qt,B,z,Tt,oe,Wt),oe.side=Ue,oe.needsUpdate=!0,Ht=!0}}Ht===!0&&(Ot.updateMultisampleRenderTarget(Q),Ot.updateRenderTargetMipmap(Q))}S.setRenderTarget(pt,ht,wt),S.setClearColor(X,W),bt!==void 0&&(z.viewport=bt),S.toneMapping=Rt}function Qi(M,U,B){const z=U.isScene===!0?U.overrideMaterial:null;for(let F=0,Q=M.length;F<Q;F++){const ot=M[F],pt=ot.object,ht=ot.geometry,wt=ot.group;let Rt=ot.material;Rt.allowOverride===!0&&z!==null&&(Rt=z),pt.layers.test(B.layers)&&Ya(pt,U,B,ht,Rt,wt)}}function Ya(M,U,B,z,F,Q){M.onBeforeRender(S,U,B,z,F,Q),M.modelViewMatrix.multiplyMatrices(B.matrixWorldInverse,M.matrixWorld),M.normalMatrix.getNormalMatrix(M.modelViewMatrix),F.onBeforeRender(S,U,B,z,M,Q),F.transparent===!0&&F.side===sn&&F.forceSinglePass===!1?(F.side=Ie,F.needsUpdate=!0,S.renderBufferDirect(B,U,z,F,M,Q),F.side=Dn,F.needsUpdate=!0,S.renderBufferDirect(B,U,z,F,M,Q),F.side=sn):S.renderBufferDirect(B,U,z,F,M,Q),M.onAfterRender(S,U,B,z,F,Q)}function ts(M,U,B){U.isScene!==!0&&(U=St);const z=_t.get(M),F=f.state.lights,Q=f.state.shadowsArray,ot=F.state.version,pt=G.getParameters(M,F.state,Q,U,B),ht=G.getProgramCacheKey(pt);let wt=z.programs;z.environment=M.isMeshStandardMaterial?U.environment:null,z.fog=U.fog,z.envMap=(M.isMeshStandardMaterial?he:xe).get(M.envMap||z.environment),z.envMapRotation=z.environment!==null&&M.envMap===null?U.environmentRotation:M.envMapRotation,wt===void 0&&(M.addEventListener("dispose",j),wt=new Map,z.programs=wt);let Rt=wt.get(ht);if(Rt!==void 0){if(z.currentProgram===Rt&&z.lightsStateVersion===ot)return Ka(M,pt),Rt}else pt.uniforms=G.getUniforms(M),M.onBeforeCompile(pt,S),Rt=G.acquireProgram(pt,ht),wt.set(ht,Rt),z.uniforms=pt.uniforms;const bt=z.uniforms;return(!M.isShaderMaterial&&!M.isRawShaderMaterial||M.clipping===!0)&&(bt.clippingPlanes=nt.uniform),Ka(M,pt),z.needsLights=nc(M),z.lightsStateVersion=ot,z.needsLights&&(bt.ambientLightColor.value=F.state.ambient,bt.lightProbe.value=F.state.probe,bt.directionalLights.value=F.state.directional,bt.directionalLightShadows.value=F.state.directionalShadow,bt.spotLights.value=F.state.spot,bt.spotLightShadows.value=F.state.spotShadow,bt.rectAreaLights.value=F.state.rectArea,bt.ltc_1.value=F.state.rectAreaLTC1,bt.ltc_2.value=F.state.rectAreaLTC2,bt.pointLights.value=F.state.point,bt.pointLightShadows.value=F.state.pointShadow,bt.hemisphereLights.value=F.state.hemi,bt.directionalShadowMap.value=F.state.directionalShadowMap,bt.directionalShadowMatrix.value=F.state.directionalShadowMatrix,bt.spotShadowMap.value=F.state.spotShadowMap,bt.spotLightMatrix.value=F.state.spotLightMatrix,bt.spotLightMap.value=F.state.spotLightMap,bt.pointShadowMap.value=F.state.pointShadowMap,bt.pointShadowMatrix.value=F.state.pointShadowMatrix),z.currentProgram=Rt,z.uniformsList=null,Rt}function ja(M){if(M.uniformsList===null){const U=M.currentProgram.getUniforms();M.uniformsList=Us.seqWithValue(U.seq,M.uniforms)}return M.uniformsList}function Ka(M,U){const B=_t.get(M);B.outputColorSpace=U.outputColorSpace,B.batching=U.batching,B.batchingColor=U.batchingColor,B.instancing=U.instancing,B.instancingColor=U.instancingColor,B.instancingMorph=U.instancingMorph,B.skinning=U.skinning,B.morphTargets=U.morphTargets,B.morphNormals=U.morphNormals,B.morphColors=U.morphColors,B.morphTargetsCount=U.morphTargetsCount,B.numClippingPlanes=U.numClippingPlanes,B.numIntersection=U.numClipIntersection,B.vertexAlphas=U.vertexAlphas,B.vertexTangents=U.vertexTangents,B.toneMapping=U.toneMapping}function tc(M,U,B,z,F){U.isScene!==!0&&(U=St),Ot.resetTextureUnits();const Q=U.fog,ot=z.isMeshStandardMaterial?U.environment:null,pt=D===null?S.outputColorSpace:D.isXRRenderTarget===!0?D.texture.colorSpace:Ei,ht=(z.isMeshStandardMaterial?he:xe).get(z.envMap||ot),wt=z.vertexColors===!0&&!!B.attributes.color&&B.attributes.color.itemSize===4,Rt=!!B.attributes.tangent&&(!!z.normalMap||z.anisotropy>0),bt=!!B.morphAttributes.position,Ht=!!B.morphAttributes.normal,Zt=!!B.morphAttributes.color;let ce=Ln;z.toneMapped&&(D===null||D.isXRRenderTarget===!0)&&(ce=S.toneMapping);const ie=B.morphAttributes.position||B.morphAttributes.normal||B.morphAttributes.color,Qt=ie!==void 0?ie.length:0,Tt=_t.get(z),oe=f.state.lights;if(qt===!0&&(Y===!0||M!==x)){const Te=M===x&&z.id===y;nt.setState(z,M,Te)}let Wt=!1;z.version===Tt.__version?(Tt.needsLights&&Tt.lightsStateVersion!==oe.state.version||Tt.outputColorSpace!==pt||F.isBatchedMesh&&Tt.batching===!1||!F.isBatchedMesh&&Tt.batching===!0||F.isBatchedMesh&&Tt.batchingColor===!0&&F.colorTexture===null||F.isBatchedMesh&&Tt.batchingColor===!1&&F.colorTexture!==null||F.isInstancedMesh&&Tt.instancing===!1||!F.isInstancedMesh&&Tt.instancing===!0||F.isSkinnedMesh&&Tt.skinning===!1||!F.isSkinnedMesh&&Tt.skinning===!0||F.isInstancedMesh&&Tt.instancingColor===!0&&F.instanceColor===null||F.isInstancedMesh&&Tt.instancingColor===!1&&F.instanceColor!==null||F.isInstancedMesh&&Tt.instancingMorph===!0&&F.morphTexture===null||F.isInstancedMesh&&Tt.instancingMorph===!1&&F.morphTexture!==null||Tt.envMap!==ht||z.fog===!0&&Tt.fog!==Q||Tt.numClippingPlanes!==void 0&&(Tt.numClippingPlanes!==nt.numPlanes||Tt.numIntersection!==nt.numIntersection)||Tt.vertexAlphas!==wt||Tt.vertexTangents!==Rt||Tt.morphTargets!==bt||Tt.morphNormals!==Ht||Tt.morphColors!==Zt||Tt.toneMapping!==ce||Tt.morphTargetsCount!==Qt)&&(Wt=!0):(Wt=!0,Tt.__version=z.version);let Ue=Tt.currentProgram;Wt===!0&&(Ue=ts(z,U,F));let ti=!1,Ne=!1,Ri=!1;const le=Ue.getUniforms(),ke=Tt.uniforms;if(gt.useProgram(Ue.program)&&(ti=!0,Ne=!0,Ri=!0),z.id!==y&&(y=z.id,Ne=!0),ti||x!==M){gt.buffers.depth.getReversed()&&M.reversedDepth!==!0&&(M._reversedDepth=!0,M.updateProjectionMatrix()),le.setValue(R,"projectionMatrix",M.projectionMatrix),le.setValue(R,"viewMatrix",M.matrixWorldInverse);const Pe=le.map.cameraPosition;Pe!==void 0&&Pe.setValue(R,ft.setFromMatrixPosition(M.matrixWorld)),Ct.logarithmicDepthBuffer&&le.setValue(R,"logDepthBufFC",2/(Math.log(M.far+1)/Math.LN2)),(z.isMeshPhongMaterial||z.isMeshToonMaterial||z.isMeshLambertMaterial||z.isMeshBasicMaterial||z.isMeshStandardMaterial||z.isShaderMaterial)&&le.setValue(R,"isOrthographic",M.isOrthographicCamera===!0),x!==M&&(x=M,Ne=!0,Ri=!0)}if(F.isSkinnedMesh){le.setOptional(R,F,"bindMatrix"),le.setOptional(R,F,"bindMatrixInverse");const Te=F.skeleton;Te&&(Te.boneTexture===null&&Te.computeBoneTexture(),le.setValue(R,"boneTexture",Te.boneTexture,Ot))}F.isBatchedMesh&&(le.setOptional(R,F,"batchingTexture"),le.setValue(R,"batchingTexture",F._matricesTexture,Ot),le.setOptional(R,F,"batchingIdTexture"),le.setValue(R,"batchingIdTexture",F._indirectTexture,Ot),le.setOptional(R,F,"batchingColorTexture"),F._colorsTexture!==null&&le.setValue(R,"batchingColorTexture",F._colorsTexture,Ot));const He=B.morphAttributes;if((He.position!==void 0||He.normal!==void 0||He.color!==void 0)&&tt.update(F,B,Ue),(Ne||Tt.receiveShadow!==F.receiveShadow)&&(Tt.receiveShadow=F.receiveShadow,le.setValue(R,"receiveShadow",F.receiveShadow)),z.isMeshGouraudMaterial&&z.envMap!==null&&(ke.envMap.value=ht,ke.flipEnvMap.value=ht.isCubeTexture&&ht.isRenderTargetTexture===!1?-1:1),z.isMeshStandardMaterial&&z.envMap===null&&U.environment!==null&&(ke.envMapIntensity.value=U.environmentIntensity),Ne&&(le.setValue(R,"toneMappingExposure",S.toneMappingExposure),Tt.needsLights&&ec(ke,Ri),Q&&z.fog===!0&&K.refreshFogUniforms(ke,Q),K.refreshMaterialUniforms(ke,z,H,Z,f.state.transmissionRenderTarget[M.id]),Us.upload(R,ja(Tt),ke,Ot)),z.isShaderMaterial&&z.uniformsNeedUpdate===!0&&(Us.upload(R,ja(Tt),ke,Ot),z.uniformsNeedUpdate=!1),z.isSpriteMaterial&&le.setValue(R,"center",F.center),le.setValue(R,"modelViewMatrix",F.modelViewMatrix),le.setValue(R,"normalMatrix",F.normalMatrix),le.setValue(R,"modelMatrix",F.matrixWorld),z.isShaderMaterial||z.isRawShaderMaterial){const Te=z.uniformsGroups;for(let Pe=0,qs=Te.length;Pe<qs;Pe++){const Un=Te[Pe];Ut.update(Un,Ue),Ut.bind(Un,Ue)}}return Ue}function ec(M,U){M.ambientLightColor.needsUpdate=U,M.lightProbe.needsUpdate=U,M.directionalLights.needsUpdate=U,M.directionalLightShadows.needsUpdate=U,M.pointLights.needsUpdate=U,M.pointLightShadows.needsUpdate=U,M.spotLights.needsUpdate=U,M.spotLightShadows.needsUpdate=U,M.rectAreaLights.needsUpdate=U,M.hemisphereLights.needsUpdate=U}function nc(M){return M.isMeshLambertMaterial||M.isMeshToonMaterial||M.isMeshPhongMaterial||M.isMeshStandardMaterial||M.isShadowMaterial||M.isShaderMaterial&&M.lights===!0}this.getActiveCubeFace=function(){return A},this.getActiveMipmapLevel=function(){return C},this.getRenderTarget=function(){return D},this.setRenderTargetTextures=function(M,U,B){const z=_t.get(M);z.__autoAllocateDepthBuffer=M.resolveDepthBuffer===!1,z.__autoAllocateDepthBuffer===!1&&(z.__useRenderToTexture=!1),_t.get(M.texture).__webglTexture=U,_t.get(M.depthTexture).__webglTexture=z.__autoAllocateDepthBuffer?void 0:B,z.__hasExternalTextures=!0},this.setRenderTargetFramebuffer=function(M,U){const B=_t.get(M);B.__webglFramebuffer=U,B.__useDefaultFramebuffer=U===void 0};const ic=R.createFramebuffer();this.setRenderTarget=function(M,U=0,B=0){D=M,A=U,C=B;let z=!0,F=null,Q=!1,ot=!1;if(M){const ht=_t.get(M);if(ht.__useDefaultFramebuffer!==void 0)gt.bindFramebuffer(R.FRAMEBUFFER,null),z=!1;else if(ht.__webglFramebuffer===void 0)Ot.setupRenderTarget(M);else if(ht.__hasExternalTextures)Ot.rebindTextures(M,_t.get(M.texture).__webglTexture,_t.get(M.depthTexture).__webglTexture);else if(M.depthBuffer){const bt=M.depthTexture;if(ht.__boundDepthTexture!==bt){if(bt!==null&&_t.has(bt)&&(M.width!==bt.image.width||M.height!==bt.image.height))throw new Error("WebGLRenderTarget: Attached DepthTexture is initialized to the incorrect size.");Ot.setupDepthRenderbuffer(M)}}const wt=M.texture;(wt.isData3DTexture||wt.isDataArrayTexture||wt.isCompressedArrayTexture)&&(ot=!0);const Rt=_t.get(M).__webglFramebuffer;M.isWebGLCubeRenderTarget?(Array.isArray(Rt[U])?F=Rt[U][B]:F=Rt[U],Q=!0):M.samples>0&&Ot.useMultisampledRTT(M)===!1?F=_t.get(M).__webglMultisampledFramebuffer:Array.isArray(Rt)?F=Rt[B]:F=Rt,L.copy(M.viewport),N.copy(M.scissor),k=M.scissorTest}else L.copy(Et).multiplyScalar(H).floor(),N.copy(kt).multiplyScalar(H).floor(),k=te;if(B!==0&&(F=ic),gt.bindFramebuffer(R.FRAMEBUFFER,F)&&z&&gt.drawBuffers(M,F),gt.viewport(L),gt.scissor(N),gt.setScissorTest(k),Q){const ht=_t.get(M.texture);R.framebufferTexture2D(R.FRAMEBUFFER,R.COLOR_ATTACHMENT0,R.TEXTURE_CUBE_MAP_POSITIVE_X+U,ht.__webglTexture,B)}else if(ot){const ht=U;for(let wt=0;wt<M.textures.length;wt++){const Rt=_t.get(M.textures[wt]);R.framebufferTextureLayer(R.FRAMEBUFFER,R.COLOR_ATTACHMENT0+wt,Rt.__webglTexture,B,ht)}}else if(M!==null&&B!==0){const ht=_t.get(M.texture);R.framebufferTexture2D(R.FRAMEBUFFER,R.COLOR_ATTACHMENT0,R.TEXTURE_2D,ht.__webglTexture,B)}y=-1},this.readRenderTargetPixels=function(M,U,B,z,F,Q,ot,pt=0){if(!(M&&M.isWebGLRenderTarget)){console.error("THREE.WebGLRenderer.readRenderTargetPixels: renderTarget is not THREE.WebGLRenderTarget.");return}let ht=_t.get(M).__webglFramebuffer;if(M.isWebGLCubeRenderTarget&&ot!==void 0&&(ht=ht[ot]),ht){gt.bindFramebuffer(R.FRAMEBUFFER,ht);try{const wt=M.textures[pt],Rt=wt.format,bt=wt.type;if(!Ct.textureFormatReadable(Rt)){console.error("THREE.WebGLRenderer.readRenderTargetPixels: renderTarget is not in RGBA or implementation defined format.");return}if(!Ct.textureTypeReadable(bt)){console.error("THREE.WebGLRenderer.readRenderTargetPixels: renderTarget is not in UnsignedByteType or implementation defined type.");return}U>=0&&U<=M.width-z&&B>=0&&B<=M.height-F&&(M.textures.length>1&&R.readBuffer(R.COLOR_ATTACHMENT0+pt),R.readPixels(U,B,z,F,yt.convert(Rt),yt.convert(bt),Q))}finally{const wt=D!==null?_t.get(D).__webglFramebuffer:null;gt.bindFramebuffer(R.FRAMEBUFFER,wt)}}},this.readRenderTargetPixelsAsync=async function(M,U,B,z,F,Q,ot,pt=0){if(!(M&&M.isWebGLRenderTarget))throw new Error("THREE.WebGLRenderer.readRenderTargetPixels: renderTarget is not THREE.WebGLRenderTarget.");let ht=_t.get(M).__webglFramebuffer;if(M.isWebGLCubeRenderTarget&&ot!==void 0&&(ht=ht[ot]),ht)if(U>=0&&U<=M.width-z&&B>=0&&B<=M.height-F){gt.bindFramebuffer(R.FRAMEBUFFER,ht);const wt=M.textures[pt],Rt=wt.format,bt=wt.type;if(!Ct.textureFormatReadable(Rt))throw new Error("THREE.WebGLRenderer.readRenderTargetPixelsAsync: renderTarget is not in RGBA or implementation defined format.");if(!Ct.textureTypeReadable(bt))throw new Error("THREE.WebGLRenderer.readRenderTargetPixelsAsync: renderTarget is not in UnsignedByteType or implementation defined type.");const Ht=R.createBuffer();R.bindBuffer(R.PIXEL_PACK_BUFFER,Ht),R.bufferData(R.PIXEL_PACK_BUFFER,Q.byteLength,R.STREAM_READ),M.textures.length>1&&R.readBuffer(R.COLOR_ATTACHMENT0+pt),R.readPixels(U,B,z,F,yt.convert(Rt),yt.convert(bt),0);const Zt=D!==null?_t.get(D).__webglFramebuffer:null;gt.bindFramebuffer(R.FRAMEBUFFER,Zt);const ce=R.fenceSync(R.SYNC_GPU_COMMANDS_COMPLETE,0);return R.flush(),await nh(R,ce,4),R.bindBuffer(R.PIXEL_PACK_BUFFER,Ht),R.getBufferSubData(R.PIXEL_PACK_BUFFER,0,Q),R.deleteBuffer(Ht),R.deleteSync(ce),Q}else throw new Error("THREE.WebGLRenderer.readRenderTargetPixelsAsync: requested read bounds are out of range.")},this.copyFramebufferToTexture=function(M,U=null,B=0){const z=Math.pow(2,-B),F=Math.floor(M.image.width*z),Q=Math.floor(M.image.height*z),ot=U!==null?U.x:0,pt=U!==null?U.y:0;Ot.setTexture2D(M,0),R.copyTexSubImage2D(R.TEXTURE_2D,B,0,0,ot,pt,F,Q),gt.unbindTexture()};const sc=R.createFramebuffer(),rc=R.createFramebuffer();this.copyTextureToTexture=function(M,U,B=null,z=null,F=0,Q=null){Q===null&&(F!==0?(Ki("WebGLRenderer: copyTextureToTexture function signature has changed to support src and dst mipmap levels."),Q=F,F=0):Q=0);let ot,pt,ht,wt,Rt,bt,Ht,Zt,ce;const ie=M.isCompressedTexture?M.mipmaps[Q]:M.image;if(B!==null)ot=B.max.x-B.min.x,pt=B.max.y-B.min.y,ht=B.isBox3?B.max.z-B.min.z:1,wt=B.min.x,Rt=B.min.y,bt=B.isBox3?B.min.z:0;else{const He=Math.pow(2,-F);ot=Math.floor(ie.width*He),pt=Math.floor(ie.height*He),M.isDataArrayTexture?ht=ie.depth:M.isData3DTexture?ht=Math.floor(ie.depth*He):ht=1,wt=0,Rt=0,bt=0}z!==null?(Ht=z.x,Zt=z.y,ce=z.z):(Ht=0,Zt=0,ce=0);const Qt=yt.convert(U.format),Tt=yt.convert(U.type);let oe;U.isData3DTexture?(Ot.setTexture3D(U,0),oe=R.TEXTURE_3D):U.isDataArrayTexture||U.isCompressedArrayTexture?(Ot.setTexture2DArray(U,0),oe=R.TEXTURE_2D_ARRAY):(Ot.setTexture2D(U,0),oe=R.TEXTURE_2D),R.pixelStorei(R.UNPACK_FLIP_Y_WEBGL,U.flipY),R.pixelStorei(R.UNPACK_PREMULTIPLY_ALPHA_WEBGL,U.premultiplyAlpha),R.pixelStorei(R.UNPACK_ALIGNMENT,U.unpackAlignment);const Wt=R.getParameter(R.UNPACK_ROW_LENGTH),Ue=R.getParameter(R.UNPACK_IMAGE_HEIGHT),ti=R.getParameter(R.UNPACK_SKIP_PIXELS),Ne=R.getParameter(R.UNPACK_SKIP_ROWS),Ri=R.getParameter(R.UNPACK_SKIP_IMAGES);R.pixelStorei(R.UNPACK_ROW_LENGTH,ie.width),R.pixelStorei(R.UNPACK_IMAGE_HEIGHT,ie.height),R.pixelStorei(R.UNPACK_SKIP_PIXELS,wt),R.pixelStorei(R.UNPACK_SKIP_ROWS,Rt),R.pixelStorei(R.UNPACK_SKIP_IMAGES,bt);const le=M.isDataArrayTexture||M.isData3DTexture,ke=U.isDataArrayTexture||U.isData3DTexture;if(M.isDepthTexture){const He=_t.get(M),Te=_t.get(U),Pe=_t.get(He.__renderTarget),qs=_t.get(Te.__renderTarget);gt.bindFramebuffer(R.READ_FRAMEBUFFER,Pe.__webglFramebuffer),gt.bindFramebuffer(R.DRAW_FRAMEBUFFER,qs.__webglFramebuffer);for(let Un=0;Un<ht;Un++)le&&(R.framebufferTextureLayer(R.READ_FRAMEBUFFER,R.COLOR_ATTACHMENT0,_t.get(M).__webglTexture,F,bt+Un),R.framebufferTextureLayer(R.DRAW_FRAMEBUFFER,R.COLOR_ATTACHMENT0,_t.get(U).__webglTexture,Q,ce+Un)),R.blitFramebuffer(wt,Rt,ot,pt,Ht,Zt,ot,pt,R.DEPTH_BUFFER_BIT,R.NEAREST);gt.bindFramebuffer(R.READ_FRAMEBUFFER,null),gt.bindFramebuffer(R.DRAW_FRAMEBUFFER,null)}else if(F!==0||M.isRenderTargetTexture||_t.has(M)){const He=_t.get(M),Te=_t.get(U);gt.bindFramebuffer(R.READ_FRAMEBUFFER,sc),gt.bindFramebuffer(R.DRAW_FRAMEBUFFER,rc);for(let Pe=0;Pe<ht;Pe++)le?R.framebufferTextureLayer(R.READ_FRAMEBUFFER,R.COLOR_ATTACHMENT0,He.__webglTexture,F,bt+Pe):R.framebufferTexture2D(R.READ_FRAMEBUFFER,R.COLOR_ATTACHMENT0,R.TEXTURE_2D,He.__webglTexture,F),ke?R.framebufferTextureLayer(R.DRAW_FRAMEBUFFER,R.COLOR_ATTACHMENT0,Te.__webglTexture,Q,ce+Pe):R.framebufferTexture2D(R.DRAW_FRAMEBUFFER,R.COLOR_ATTACHMENT0,R.TEXTURE_2D,Te.__webglTexture,Q),F!==0?R.blitFramebuffer(wt,Rt,ot,pt,Ht,Zt,ot,pt,R.COLOR_BUFFER_BIT,R.NEAREST):ke?R.copyTexSubImage3D(oe,Q,Ht,Zt,ce+Pe,wt,Rt,ot,pt):R.copyTexSubImage2D(oe,Q,Ht,Zt,wt,Rt,ot,pt);gt.bindFramebuffer(R.READ_FRAMEBUFFER,null),gt.bindFramebuffer(R.DRAW_FRAMEBUFFER,null)}else ke?M.isDataTexture||M.isData3DTexture?R.texSubImage3D(oe,Q,Ht,Zt,ce,ot,pt,ht,Qt,Tt,ie.data):U.isCompressedArrayTexture?R.compressedTexSubImage3D(oe,Q,Ht,Zt,ce,ot,pt,ht,Qt,ie.data):R.texSubImage3D(oe,Q,Ht,Zt,ce,ot,pt,ht,Qt,Tt,ie):M.isDataTexture?R.texSubImage2D(R.TEXTURE_2D,Q,Ht,Zt,ot,pt,Qt,Tt,ie.data):M.isCompressedTexture?R.compressedTexSubImage2D(R.TEXTURE_2D,Q,Ht,Zt,ie.width,ie.height,Qt,ie.data):R.texSubImage2D(R.TEXTURE_2D,Q,Ht,Zt,ot,pt,Qt,Tt,ie);R.pixelStorei(R.UNPACK_ROW_LENGTH,Wt),R.pixelStorei(R.UNPACK_IMAGE_HEIGHT,Ue),R.pixelStorei(R.UNPACK_SKIP_PIXELS,ti),R.pixelStorei(R.UNPACK_SKIP_ROWS,Ne),R.pixelStorei(R.UNPACK_SKIP_IMAGES,Ri),Q===0&&U.generateMipmaps&&R.generateMipmap(oe),gt.unbindTexture()},this.initRenderTarget=function(M){_t.get(M).__webglFramebuffer===void 0&&Ot.setupRenderTarget(M)},this.initTexture=function(M){M.isCubeTexture?Ot.setTextureCube(M,0):M.isData3DTexture?Ot.setTexture3D(M,0):M.isDataArrayTexture||M.isCompressedArrayTexture?Ot.setTexture2DArray(M,0):Ot.setTexture2D(M,0),gt.unbindTexture()},this.resetState=function(){A=0,C=0,D=null,gt.reset(),at.reset()},typeof __THREE_DEVTOOLS__<"u"&&__THREE_DEVTOOLS__.dispatchEvent(new CustomEvent("observe",{detail:this}))}get coordinateSystem(){return on}get outputColorSpace(){return this._outputColorSpace}set outputColorSpace(t){this._outputColorSpace=t;const e=this.getContext();e.drawingBufferColorSpace=Gt._getDrawingBufferColorSpace(t),e.unpackColorSpace=Gt._getUnpackColorSpace()}}const ol={type:"change"},Va={type:"start"},Jl={type:"end"},As=new $i,ll=new Cn,Um=Math.cos(70*Is.DEG2RAD),pe=new P,Le=2*Math.PI,$t={NONE:-1,ROTATE:0,DOLLY:1,PAN:2,TOUCH_ROTATE:3,TOUCH_PAN:4,TOUCH_DOLLY_PAN:5,TOUCH_DOLLY_ROTATE:6},wr=1e-6;class Nm extends Kh{constructor(t,e=null){super(t,e),this.state=$t.NONE,this.target=new P,this.cursor=new P,this.minDistance=0,this.maxDistance=1/0,this.minZoom=0,this.maxZoom=1/0,this.minTargetRadius=0,this.maxTargetRadius=1/0,this.minPolarAngle=0,this.maxPolarAngle=Math.PI,this.minAzimuthAngle=-1/0,this.maxAzimuthAngle=1/0,this.enableDamping=!1,this.dampingFactor=.05,this.enableZoom=!0,this.zoomSpeed=1,this.enableRotate=!0,this.rotateSpeed=1,this.keyRotateSpeed=1,this.enablePan=!0,this.panSpeed=1,this.screenSpacePanning=!0,this.keyPanSpeed=7,this.zoomToCursor=!1,this.autoRotate=!1,this.autoRotateSpeed=2,this.keys={LEFT:"ArrowLeft",UP:"ArrowUp",RIGHT:"ArrowRight",BOTTOM:"ArrowDown"},this.mouseButtons={LEFT:xn.ROTATE,MIDDLE:xn.DOLLY,RIGHT:xn.PAN},this.touches={ONE:Pn.ROTATE,TWO:Pn.DOLLY_PAN},this.target0=this.target.clone(),this.position0=this.object.position.clone(),this.zoom0=this.object.zoom,this._domElementKeyEvents=null,this._lastPosition=new P,this._lastQuaternion=new Kn,this._lastTargetPosition=new P,this._quat=new Kn().setFromUnitVectors(t.up,new P(0,1,0)),this._quatInverse=this._quat.clone().invert(),this._spherical=new No,this._sphericalDelta=new No,this._scale=1,this._panOffset=new P,this._rotateStart=new dt,this._rotateEnd=new dt,this._rotateDelta=new dt,this._panStart=new dt,this._panEnd=new dt,this._panDelta=new dt,this._dollyStart=new dt,this._dollyEnd=new dt,this._dollyDelta=new dt,this._dollyDirection=new P,this._mouse=new dt,this._performCursorZoom=!1,this._pointers=[],this._pointerPositions={},this._controlActive=!1,this._onPointerMove=Om.bind(this),this._onPointerDown=Fm.bind(this),this._onPointerUp=Bm.bind(this),this._onContextMenu=Xm.bind(this),this._onMouseWheel=Hm.bind(this),this._onKeyDown=Vm.bind(this),this._onTouchStart=Gm.bind(this),this._onTouchMove=Wm.bind(this),this._onMouseDown=zm.bind(this),this._onMouseMove=km.bind(this),this._interceptControlDown=qm.bind(this),this._interceptControlUp=Ym.bind(this),this.domElement!==null&&this.connect(this.domElement),this.update()}connect(t){super.connect(t),this.domElement.addEventListener("pointerdown",this._onPointerDown),this.domElement.addEventListener("pointercancel",this._onPointerUp),this.domElement.addEventListener("contextmenu",this._onContextMenu),this.domElement.addEventListener("wheel",this._onMouseWheel,{passive:!1}),this.domElement.getRootNode().addEventListener("keydown",this._interceptControlDown,{passive:!0,capture:!0}),this.domElement.style.touchAction="none"}disconnect(){this.domElement.removeEventListener("pointerdown",this._onPointerDown),this.domElement.removeEventListener("pointermove",this._onPointerMove),this.domElement.removeEventListener("pointerup",this._onPointerUp),this.domElement.removeEventListener("pointercancel",this._onPointerUp),this.domElement.removeEventListener("wheel",this._onMouseWheel),this.domElement.removeEventListener("contextmenu",this._onContextMenu),this.stopListenToKeyEvents(),this.domElement.getRootNode().removeEventListener("keydown",this._interceptControlDown,{capture:!0}),this.domElement.style.touchAction="auto"}dispose(){this.disconnect()}getPolarAngle(){return this._spherical.phi}getAzimuthalAngle(){return this._spherical.theta}getDistance(){return this.object.position.distanceTo(this.target)}listenToKeyEvents(t){t.addEventListener("keydown",this._onKeyDown),this._domElementKeyEvents=t}stopListenToKeyEvents(){this._domElementKeyEvents!==null&&(this._domElementKeyEvents.removeEventListener("keydown",this._onKeyDown),this._domElementKeyEvents=null)}saveState(){this.target0.copy(this.target),this.position0.copy(this.object.position),this.zoom0=this.object.zoom}reset(){this.target.copy(this.target0),this.object.position.copy(this.position0),this.object.zoom=this.zoom0,this.object.updateProjectionMatrix(),this.dispatchEvent(ol),this.update(),this.state=$t.NONE}update(t=null){const e=this.object.position;pe.copy(e).sub(this.target),pe.applyQuaternion(this._quat),this._spherical.setFromVector3(pe),this.autoRotate&&this.state===$t.NONE&&this._rotateLeft(this._getAutoRotationAngle(t)),this.enableDamping?(this._spherical.theta+=this._sphericalDelta.theta*this.dampingFactor,this._spherical.phi+=this._sphericalDelta.phi*this.dampingFactor):(this._spherical.theta+=this._sphericalDelta.theta,this._spherical.phi+=this._sphericalDelta.phi);let n=this.minAzimuthAngle,i=this.maxAzimuthAngle;isFinite(n)&&isFinite(i)&&(n<-Math.PI?n+=Le:n>Math.PI&&(n-=Le),i<-Math.PI?i+=Le:i>Math.PI&&(i-=Le),n<=i?this._spherical.theta=Math.max(n,Math.min(i,this._spherical.theta)):this._spherical.theta=this._spherical.theta>(n+i)/2?Math.max(n,this._spherical.theta):Math.min(i,this._spherical.theta)),this._spherical.phi=Math.max(this.minPolarAngle,Math.min(this.maxPolarAngle,this._spherical.phi)),this._spherical.makeSafe(),this.enableDamping===!0?this.target.addScaledVector(this._panOffset,this.dampingFactor):this.target.add(this._panOffset),this.target.sub(this.cursor),this.target.clampLength(this.minTargetRadius,this.maxTargetRadius),this.target.add(this.cursor);let r=!1;if(this.zoomToCursor&&this._performCursorZoom||this.object.isOrthographicCamera)this._spherical.radius=this._clampDistance(this._spherical.radius);else{const a=this._spherical.radius;this._spherical.radius=this._clampDistance(this._spherical.radius*this._scale),r=a!=this._spherical.radius}if(pe.setFromSpherical(this._spherical),pe.applyQuaternion(this._quatInverse),e.copy(this.target).add(pe),this.object.lookAt(this.target),this.enableDamping===!0?(this._sphericalDelta.theta*=1-this.dampingFactor,this._sphericalDelta.phi*=1-this.dampingFactor,this._panOffset.multiplyScalar(1-this.dampingFactor)):(this._sphericalDelta.set(0,0,0),this._panOffset.set(0,0,0)),this.zoomToCursor&&this._performCursorZoom){let a=null;if(this.object.isPerspectiveCamera){const o=pe.length();a=this._clampDistance(o*this._scale);const l=o-a;this.object.position.addScaledVector(this._dollyDirection,l),this.object.updateMatrixWorld(),r=!!l}else if(this.object.isOrthographicCamera){const o=new P(this._mouse.x,this._mouse.y,0);o.unproject(this.object);const l=this.object.zoom;this.object.zoom=Math.max(this.minZoom,Math.min(this.maxZoom,this.object.zoom/this._scale)),this.object.updateProjectionMatrix(),r=l!==this.object.zoom;const c=new P(this._mouse.x,this._mouse.y,0);c.unproject(this.object),this.object.position.sub(c).add(o),this.object.updateMatrixWorld(),a=pe.length()}else console.warn("WARNING: OrbitControls.js encountered an unknown camera type - zoom to cursor disabled."),this.zoomToCursor=!1;a!==null&&(this.screenSpacePanning?this.target.set(0,0,-1).transformDirection(this.object.matrix).multiplyScalar(a).add(this.object.position):(As.origin.copy(this.object.position),As.direction.set(0,0,-1).transformDirection(this.object.matrix),Math.abs(this.object.up.dot(As.direction))<Um?this.object.lookAt(this.target):(ll.setFromNormalAndCoplanarPoint(this.object.up,this.target),As.intersectPlane(ll,this.target))))}else if(this.object.isOrthographicCamera){const a=this.object.zoom;this.object.zoom=Math.max(this.minZoom,Math.min(this.maxZoom,this.object.zoom/this._scale)),a!==this.object.zoom&&(this.object.updateProjectionMatrix(),r=!0)}return this._scale=1,this._performCursorZoom=!1,r||this._lastPosition.distanceToSquared(this.object.position)>wr||8*(1-this._lastQuaternion.dot(this.object.quaternion))>wr||this._lastTargetPosition.distanceToSquared(this.target)>wr?(this.dispatchEvent(ol),this._lastPosition.copy(this.object.position),this._lastQuaternion.copy(this.object.quaternion),this._lastTargetPosition.copy(this.target),!0):!1}_getAutoRotationAngle(t){return t!==null?Le/60*this.autoRotateSpeed*t:Le/60/60*this.autoRotateSpeed}_getZoomScale(t){const e=Math.abs(t*.01);return Math.pow(.95,this.zoomSpeed*e)}_rotateLeft(t){this._sphericalDelta.theta-=t}_rotateUp(t){this._sphericalDelta.phi-=t}_panLeft(t,e){pe.setFromMatrixColumn(e,0),pe.multiplyScalar(-t),this._panOffset.add(pe)}_panUp(t,e){this.screenSpacePanning===!0?pe.setFromMatrixColumn(e,1):(pe.setFromMatrixColumn(e,0),pe.crossVectors(this.object.up,pe)),pe.multiplyScalar(t),this._panOffset.add(pe)}_pan(t,e){const n=this.domElement;if(this.object.isPerspectiveCamera){const i=this.object.position;pe.copy(i).sub(this.target);let r=pe.length();r*=Math.tan(this.object.fov/2*Math.PI/180),this._panLeft(2*t*r/n.clientHeight,this.object.matrix),this._panUp(2*e*r/n.clientHeight,this.object.matrix)}else this.object.isOrthographicCamera?(this._panLeft(t*(this.object.right-this.object.left)/this.object.zoom/n.clientWidth,this.object.matrix),this._panUp(e*(this.object.top-this.object.bottom)/this.object.zoom/n.clientHeight,this.object.matrix)):(console.warn("WARNING: OrbitControls.js encountered an unknown camera type - pan disabled."),this.enablePan=!1)}_dollyOut(t){this.object.isPerspectiveCamera||this.object.isOrthographicCamera?this._scale/=t:(console.warn("WARNING: OrbitControls.js encountered an unknown camera type - dolly/zoom disabled."),this.enableZoom=!1)}_dollyIn(t){this.object.isPerspectiveCamera||this.object.isOrthographicCamera?this._scale*=t:(console.warn("WARNING: OrbitControls.js encountered an unknown camera type - dolly/zoom disabled."),this.enableZoom=!1)}_updateZoomParameters(t,e){if(!this.zoomToCursor)return;this._performCursorZoom=!0;const n=this.domElement.getBoundingClientRect(),i=t-n.left,r=e-n.top,a=n.width,o=n.height;this._mouse.x=i/a*2-1,this._mouse.y=-(r/o)*2+1,this._dollyDirection.set(this._mouse.x,this._mouse.y,1).unproject(this.object).sub(this.object.position).normalize()}_clampDistance(t){return Math.max(this.minDistance,Math.min(this.maxDistance,t))}_handleMouseDownRotate(t){this._rotateStart.set(t.clientX,t.clientY)}_handleMouseDownDolly(t){this._updateZoomParameters(t.clientX,t.clientX),this._dollyStart.set(t.clientX,t.clientY)}_handleMouseDownPan(t){this._panStart.set(t.clientX,t.clientY)}_handleMouseMoveRotate(t){this._rotateEnd.set(t.clientX,t.clientY),this._rotateDelta.subVectors(this._rotateEnd,this._rotateStart).multiplyScalar(this.rotateSpeed);const e=this.domElement;this._rotateLeft(Le*this._rotateDelta.x/e.clientHeight),this._rotateUp(Le*this._rotateDelta.y/e.clientHeight),this._rotateStart.copy(this._rotateEnd),this.update()}_handleMouseMoveDolly(t){this._dollyEnd.set(t.clientX,t.clientY),this._dollyDelta.subVectors(this._dollyEnd,this._dollyStart),this._dollyDelta.y>0?this._dollyOut(this._getZoomScale(this._dollyDelta.y)):this._dollyDelta.y<0&&this._dollyIn(this._getZoomScale(this._dollyDelta.y)),this._dollyStart.copy(this._dollyEnd),this.update()}_handleMouseMovePan(t){this._panEnd.set(t.clientX,t.clientY),this._panDelta.subVectors(this._panEnd,this._panStart).multiplyScalar(this.panSpeed),this._pan(this._panDelta.x,this._panDelta.y),this._panStart.copy(this._panEnd),this.update()}_handleMouseWheel(t){this._updateZoomParameters(t.clientX,t.clientY),t.deltaY<0?this._dollyIn(this._getZoomScale(t.deltaY)):t.deltaY>0&&this._dollyOut(this._getZoomScale(t.deltaY)),this.update()}_handleKeyDown(t){let e=!1;switch(t.code){case this.keys.UP:t.ctrlKey||t.metaKey||t.shiftKey?this.enableRotate&&this._rotateUp(Le*this.keyRotateSpeed/this.domElement.clientHeight):this.enablePan&&this._pan(0,this.keyPanSpeed),e=!0;break;case this.keys.BOTTOM:t.ctrlKey||t.metaKey||t.shiftKey?this.enableRotate&&this._rotateUp(-Le*this.keyRotateSpeed/this.domElement.clientHeight):this.enablePan&&this._pan(0,-this.keyPanSpeed),e=!0;break;case this.keys.LEFT:t.ctrlKey||t.metaKey||t.shiftKey?this.enableRotate&&this._rotateLeft(Le*this.keyRotateSpeed/this.domElement.clientHeight):this.enablePan&&this._pan(this.keyPanSpeed,0),e=!0;break;case this.keys.RIGHT:t.ctrlKey||t.metaKey||t.shiftKey?this.enableRotate&&this._rotateLeft(-Le*this.keyRotateSpeed/this.domElement.clientHeight):this.enablePan&&this._pan(-this.keyPanSpeed,0),e=!0;break}e&&(t.preventDefault(),this.update())}_handleTouchStartRotate(t){if(this._pointers.length===1)this._rotateStart.set(t.pageX,t.pageY);else{const e=this._getSecondPointerPosition(t),n=.5*(t.pageX+e.x),i=.5*(t.pageY+e.y);this._rotateStart.set(n,i)}}_handleTouchStartPan(t){if(this._pointers.length===1)this._panStart.set(t.pageX,t.pageY);else{const e=this._getSecondPointerPosition(t),n=.5*(t.pageX+e.x),i=.5*(t.pageY+e.y);this._panStart.set(n,i)}}_handleTouchStartDolly(t){const e=this._getSecondPointerPosition(t),n=t.pageX-e.x,i=t.pageY-e.y,r=Math.sqrt(n*n+i*i);this._dollyStart.set(0,r)}_handleTouchStartDollyPan(t){this.enableZoom&&this._handleTouchStartDolly(t),this.enablePan&&this._handleTouchStartPan(t)}_handleTouchStartDollyRotate(t){this.enableZoom&&this._handleTouchStartDolly(t),this.enableRotate&&this._handleTouchStartRotate(t)}_handleTouchMoveRotate(t){if(this._pointers.length==1)this._rotateEnd.set(t.pageX,t.pageY);else{const n=this._getSecondPointerPosition(t),i=.5*(t.pageX+n.x),r=.5*(t.pageY+n.y);this._rotateEnd.set(i,r)}this._rotateDelta.subVectors(this._rotateEnd,this._rotateStart).multiplyScalar(this.rotateSpeed);const e=this.domElement;this._rotateLeft(Le*this._rotateDelta.x/e.clientHeight),this._rotateUp(Le*this._rotateDelta.y/e.clientHeight),this._rotateStart.copy(this._rotateEnd)}_handleTouchMovePan(t){if(this._pointers.length===1)this._panEnd.set(t.pageX,t.pageY);else{const e=this._getSecondPointerPosition(t),n=.5*(t.pageX+e.x),i=.5*(t.pageY+e.y);this._panEnd.set(n,i)}this._panDelta.subVectors(this._panEnd,this._panStart).multiplyScalar(this.panSpeed),this._pan(this._panDelta.x,this._panDelta.y),this._panStart.copy(this._panEnd)}_handleTouchMoveDolly(t){const e=this._getSecondPointerPosition(t),n=t.pageX-e.x,i=t.pageY-e.y,r=Math.sqrt(n*n+i*i);this._dollyEnd.set(0,r),this._dollyDelta.set(0,Math.pow(this._dollyEnd.y/this._dollyStart.y,this.zoomSpeed)),this._dollyOut(this._dollyDelta.y),this._dollyStart.copy(this._dollyEnd);const a=(t.pageX+e.x)*.5,o=(t.pageY+e.y)*.5;this._updateZoomParameters(a,o)}_handleTouchMoveDollyPan(t){this.enableZoom&&this._handleTouchMoveDolly(t),this.enablePan&&this._handleTouchMovePan(t)}_handleTouchMoveDollyRotate(t){this.enableZoom&&this._handleTouchMoveDolly(t),this.enableRotate&&this._handleTouchMoveRotate(t)}_addPointer(t){this._pointers.push(t.pointerId)}_removePointer(t){delete this._pointerPositions[t.pointerId];for(let e=0;e<this._pointers.length;e++)if(this._pointers[e]==t.pointerId){this._pointers.splice(e,1);return}}_isTrackingPointer(t){for(let e=0;e<this._pointers.length;e++)if(this._pointers[e]==t.pointerId)return!0;return!1}_trackPointer(t){let e=this._pointerPositions[t.pointerId];e===void 0&&(e=new dt,this._pointerPositions[t.pointerId]=e),e.set(t.pageX,t.pageY)}_getSecondPointerPosition(t){const e=t.pointerId===this._pointers[0]?this._pointers[1]:this._pointers[0];return this._pointerPositions[e]}_customWheelEvent(t){const e=t.deltaMode,n={clientX:t.clientX,clientY:t.clientY,deltaY:t.deltaY};switch(e){case 1:n.deltaY*=16;break;case 2:n.deltaY*=100;break}return t.ctrlKey&&!this._controlActive&&(n.deltaY*=10),n}}function Fm(s){this.enabled!==!1&&(this._pointers.length===0&&(this.domElement.setPointerCapture(s.pointerId),this.domElement.addEventListener("pointermove",this._onPointerMove),this.domElement.addEventListener("pointerup",this._onPointerUp)),!this._isTrackingPointer(s)&&(this._addPointer(s),s.pointerType==="touch"?this._onTouchStart(s):this._onMouseDown(s)))}function Om(s){this.enabled!==!1&&(s.pointerType==="touch"?this._onTouchMove(s):this._onMouseMove(s))}function Bm(s){switch(this._removePointer(s),this._pointers.length){case 0:this.domElement.releasePointerCapture(s.pointerId),this.domElement.removeEventListener("pointermove",this._onPointerMove),this.domElement.removeEventListener("pointerup",this._onPointerUp),this.dispatchEvent(Jl),this.state=$t.NONE;break;case 1:const t=this._pointers[0],e=this._pointerPositions[t];this._onTouchStart({pointerId:t,pageX:e.x,pageY:e.y});break}}function zm(s){let t;switch(s.button){case 0:t=this.mouseButtons.LEFT;break;case 1:t=this.mouseButtons.MIDDLE;break;case 2:t=this.mouseButtons.RIGHT;break;default:t=-1}switch(t){case xn.DOLLY:if(this.enableZoom===!1)return;this._handleMouseDownDolly(s),this.state=$t.DOLLY;break;case xn.ROTATE:if(s.ctrlKey||s.metaKey||s.shiftKey){if(this.enablePan===!1)return;this._handleMouseDownPan(s),this.state=$t.PAN}else{if(this.enableRotate===!1)return;this._handleMouseDownRotate(s),this.state=$t.ROTATE}break;case xn.PAN:if(s.ctrlKey||s.metaKey||s.shiftKey){if(this.enableRotate===!1)return;this._handleMouseDownRotate(s),this.state=$t.ROTATE}else{if(this.enablePan===!1)return;this._handleMouseDownPan(s),this.state=$t.PAN}break;default:this.state=$t.NONE}this.state!==$t.NONE&&this.dispatchEvent(Va)}function km(s){switch(this.state){case $t.ROTATE:if(this.enableRotate===!1)return;this._handleMouseMoveRotate(s);break;case $t.DOLLY:if(this.enableZoom===!1)return;this._handleMouseMoveDolly(s);break;case $t.PAN:if(this.enablePan===!1)return;this._handleMouseMovePan(s);break}}function Hm(s){this.enabled===!1||this.enableZoom===!1||this.state!==$t.NONE||(s.preventDefault(),this.dispatchEvent(Va),this._handleMouseWheel(this._customWheelEvent(s)),this.dispatchEvent(Jl))}function Vm(s){this.enabled!==!1&&this._handleKeyDown(s)}function Gm(s){switch(this._trackPointer(s),this._pointers.length){case 1:switch(this.touches.ONE){case Pn.ROTATE:if(this.enableRotate===!1)return;this._handleTouchStartRotate(s),this.state=$t.TOUCH_ROTATE;break;case Pn.PAN:if(this.enablePan===!1)return;this._handleTouchStartPan(s),this.state=$t.TOUCH_PAN;break;default:this.state=$t.NONE}break;case 2:switch(this.touches.TWO){case Pn.DOLLY_PAN:if(this.enableZoom===!1&&this.enablePan===!1)return;this._handleTouchStartDollyPan(s),this.state=$t.TOUCH_DOLLY_PAN;break;case Pn.DOLLY_ROTATE:if(this.enableZoom===!1&&this.enableRotate===!1)return;this._handleTouchStartDollyRotate(s),this.state=$t.TOUCH_DOLLY_ROTATE;break;default:this.state=$t.NONE}break;default:this.state=$t.NONE}this.state!==$t.NONE&&this.dispatchEvent(Va)}function Wm(s){switch(this._trackPointer(s),this.state){case $t.TOUCH_ROTATE:if(this.enableRotate===!1)return;this._handleTouchMoveRotate(s),this.update();break;case $t.TOUCH_PAN:if(this.enablePan===!1)return;this._handleTouchMovePan(s),this.update();break;case $t.TOUCH_DOLLY_PAN:if(this.enableZoom===!1&&this.enablePan===!1)return;this._handleTouchMoveDollyPan(s),this.update();break;case $t.TOUCH_DOLLY_ROTATE:if(this.enableZoom===!1&&this.enableRotate===!1)return;this._handleTouchMoveDollyRotate(s),this.update();break;default:this.state=$t.NONE}}function Xm(s){this.enabled!==!1&&s.preventDefault()}function qm(s){s.key==="Control"&&(this._controlActive=!0,this.domElement.getRootNode().addEventListener("keyup",this._interceptControlUp,{passive:!0,capture:!0}))}function Ym(s){s.key==="Control"&&(this._controlActive=!1,this.domElement.getRootNode().removeEventListener("keyup",this._interceptControlUp,{passive:!0,capture:!0}))}const Ns={name:"CopyShader",uniforms:{tDiffuse:{value:null},opacity:{value:1}},vertexShader:`

		varying vec2 vUv;

		void main() {

			vUv = uv;
			gl_Position = projectionMatrix * modelViewMatrix * vec4( position, 1.0 );

		}`,fragmentShader:`

		uniform float opacity;

		uniform sampler2D tDiffuse;

		varying vec2 vUv;

		void main() {

			vec4 texel = texture2D( tDiffuse, vUv );
			gl_FragColor = opacity * texel;


		}`};class Ci{constructor(){this.isPass=!0,this.enabled=!0,this.needsSwap=!0,this.clear=!1,this.renderToScreen=!1}setSize(){}render(){console.error("THREE.Pass: .render() must be implemented in derived pass.")}dispose(){}}const jm=new Gs(-1,1,1,-1,0,1);class Km extends me{constructor(){super(),this.setAttribute("position",new Xt([-1,3,0,-1,-1,0,3,-1,0],3)),this.setAttribute("uv",new Xt([0,2,0,0,2,0],2))}}const Zm=new Km;class Ga{constructor(t){this._mesh=new Me(Zm,t)}dispose(){this._mesh.geometry.dispose()}render(t){t.render(this._mesh,jm)}get material(){return this._mesh.material}set material(t){this._mesh.material=t}}class $m extends Ci{constructor(t,e="tDiffuse"){super(),this.textureID=e,this.uniforms=null,this.material=null,t instanceof Ce?(this.uniforms=t.uniforms,this.material=t):t&&(this.uniforms=Zi.clone(t.uniforms),this.material=new Ce({name:t.name!==void 0?t.name:"unspecified",defines:Object.assign({},t.defines),uniforms:this.uniforms,vertexShader:t.vertexShader,fragmentShader:t.fragmentShader})),this._fsQuad=new Ga(this.material)}render(t,e,n){this.uniforms[this.textureID]&&(this.uniforms[this.textureID].value=n.texture),this._fsQuad.material=this.material,this.renderToScreen?(t.setRenderTarget(null),this._fsQuad.render(t)):(t.setRenderTarget(e),this.clear&&t.clear(t.autoClearColor,t.autoClearDepth,t.autoClearStencil),this._fsQuad.render(t))}dispose(){this.material.dispose(),this._fsQuad.dispose()}}class cl extends Ci{constructor(t,e){super(),this.scene=t,this.camera=e,this.clear=!0,this.needsSwap=!1,this.inverse=!1}render(t,e,n){const i=t.getContext(),r=t.state;r.buffers.color.setMask(!1),r.buffers.depth.setMask(!1),r.buffers.color.setLocked(!0),r.buffers.depth.setLocked(!0);let a,o;this.inverse?(a=0,o=1):(a=1,o=0),r.buffers.stencil.setTest(!0),r.buffers.stencil.setOp(i.REPLACE,i.REPLACE,i.REPLACE),r.buffers.stencil.setFunc(i.ALWAYS,a,4294967295),r.buffers.stencil.setClear(o),r.buffers.stencil.setLocked(!0),t.setRenderTarget(n),this.clear&&t.clear(),t.render(this.scene,this.camera),t.setRenderTarget(e),this.clear&&t.clear(),t.render(this.scene,this.camera),r.buffers.color.setLocked(!1),r.buffers.depth.setLocked(!1),r.buffers.color.setMask(!0),r.buffers.depth.setMask(!0),r.buffers.stencil.setLocked(!1),r.buffers.stencil.setFunc(i.EQUAL,1,4294967295),r.buffers.stencil.setOp(i.KEEP,i.KEEP,i.KEEP),r.buffers.stencil.setLocked(!0)}}class Jm extends Ci{constructor(){super(),this.needsSwap=!1}render(t){t.state.buffers.stencil.setLocked(!1),t.state.buffers.stencil.setTest(!1)}}class Qm{constructor(t,e){if(this.renderer=t,this._pixelRatio=t.getPixelRatio(),e===void 0){const n=t.getSize(new dt);this._width=n.width,this._height=n.height,e=new $e(this._width*this._pixelRatio,this._height*this._pixelRatio,{type:yn}),e.texture.name="EffectComposer.rt1"}else this._width=e.width,this._height=e.height;this.renderTarget1=e,this.renderTarget2=e.clone(),this.renderTarget2.texture.name="EffectComposer.rt2",this.writeBuffer=this.renderTarget1,this.readBuffer=this.renderTarget2,this.renderToScreen=!0,this.passes=[],this.copyPass=new $m(Ns),this.copyPass.material.blending=vn,this.clock=new ql}swapBuffers(){const t=this.readBuffer;this.readBuffer=this.writeBuffer,this.writeBuffer=t}addPass(t){this.passes.push(t),t.setSize(this._width*this._pixelRatio,this._height*this._pixelRatio)}insertPass(t,e){this.passes.splice(e,0,t),t.setSize(this._width*this._pixelRatio,this._height*this._pixelRatio)}removePass(t){const e=this.passes.indexOf(t);e!==-1&&this.passes.splice(e,1)}isLastEnabledPass(t){for(let e=t+1;e<this.passes.length;e++)if(this.passes[e].enabled)return!1;return!0}render(t){t===void 0&&(t=this.clock.getDelta());const e=this.renderer.getRenderTarget();let n=!1;for(let i=0,r=this.passes.length;i<r;i++){const a=this.passes[i];if(a.enabled!==!1){if(a.renderToScreen=this.renderToScreen&&this.isLastEnabledPass(i),a.render(this.renderer,this.writeBuffer,this.readBuffer,t,n),a.needsSwap){if(n){const o=this.renderer.getContext(),l=this.renderer.state.buffers.stencil;l.setFunc(o.NOTEQUAL,1,4294967295),this.copyPass.render(this.renderer,this.writeBuffer,this.readBuffer,t),l.setFunc(o.EQUAL,1,4294967295)}this.swapBuffers()}cl!==void 0&&(a instanceof cl?n=!0:a instanceof Jm&&(n=!1))}}this.renderer.setRenderTarget(e)}reset(t){if(t===void 0){const e=this.renderer.getSize(new dt);this._pixelRatio=this.renderer.getPixelRatio(),this._width=e.width,this._height=e.height,t=this.renderTarget1.clone(),t.setSize(this._width*this._pixelRatio,this._height*this._pixelRatio)}this.renderTarget1.dispose(),this.renderTarget2.dispose(),this.renderTarget1=t,this.renderTarget2=t.clone(),this.writeBuffer=this.renderTarget1,this.readBuffer=this.renderTarget2}setSize(t,e){this._width=t,this._height=e;const n=this._width*this._pixelRatio,i=this._height*this._pixelRatio;this.renderTarget1.setSize(n,i),this.renderTarget2.setSize(n,i);for(let r=0;r<this.passes.length;r++)this.passes[r].setSize(n,i)}setPixelRatio(t){this._pixelRatio=t,this.setSize(this._width,this._height)}dispose(){this.renderTarget1.dispose(),this.renderTarget2.dispose(),this.copyPass.dispose()}}class tg extends Ci{constructor(t,e,n=null,i=null,r=null){super(),this.scene=t,this.camera=e,this.overrideMaterial=n,this.clearColor=i,this.clearAlpha=r,this.clear=!0,this.clearDepth=!1,this.needsSwap=!1,this._oldClearColor=new Ft}render(t,e,n){const i=t.autoClear;t.autoClear=!1;let r,a;this.overrideMaterial!==null&&(a=this.scene.overrideMaterial,this.scene.overrideMaterial=this.overrideMaterial),this.clearColor!==null&&(t.getClearColor(this._oldClearColor),t.setClearColor(this.clearColor,t.getClearAlpha())),this.clearAlpha!==null&&(r=t.getClearAlpha(),t.setClearAlpha(this.clearAlpha)),this.clearDepth==!0&&t.clearDepth(),t.setRenderTarget(this.renderToScreen?null:n),this.clear===!0&&t.clear(t.autoClearColor,t.autoClearDepth,t.autoClearStencil),t.render(this.scene,this.camera),this.clearColor!==null&&t.setClearColor(this._oldClearColor),this.clearAlpha!==null&&t.setClearAlpha(r),this.overrideMaterial!==null&&(this.scene.overrideMaterial=a),t.autoClear=i}}const eg={uniforms:{tDiffuse:{value:null},luminosityThreshold:{value:1},smoothWidth:{value:1},defaultColor:{value:new Ft(0)},defaultOpacity:{value:0}},vertexShader:`

		varying vec2 vUv;

		void main() {

			vUv = uv;

			gl_Position = projectionMatrix * modelViewMatrix * vec4( position, 1.0 );

		}`,fragmentShader:`

		uniform sampler2D tDiffuse;
		uniform vec3 defaultColor;
		uniform float defaultOpacity;
		uniform float luminosityThreshold;
		uniform float smoothWidth;

		varying vec2 vUv;

		void main() {

			vec4 texel = texture2D( tDiffuse, vUv );

			float v = luminance( texel.xyz );

			vec4 outputColor = vec4( defaultColor.rgb, defaultOpacity );

			float alpha = smoothstep( luminosityThreshold, luminosityThreshold + smoothWidth, v );

			gl_FragColor = mix( outputColor, texel, alpha );

		}`};class Ti extends Ci{constructor(t,e=1,n,i){super(),this.strength=e,this.radius=n,this.threshold=i,this.resolution=t!==void 0?new dt(t.x,t.y):new dt(256,256),this.clearColor=new Ft(0,0,0),this.needsSwap=!1,this.renderTargetsHorizontal=[],this.renderTargetsVertical=[],this.nMips=5;let r=Math.round(this.resolution.x/2),a=Math.round(this.resolution.y/2);this.renderTargetBright=new $e(r,a,{type:yn}),this.renderTargetBright.texture.name="UnrealBloomPass.bright",this.renderTargetBright.texture.generateMipmaps=!1;for(let h=0;h<this.nMips;h++){const d=new $e(r,a,{type:yn});d.texture.name="UnrealBloomPass.h"+h,d.texture.generateMipmaps=!1,this.renderTargetsHorizontal.push(d);const u=new $e(r,a,{type:yn});u.texture.name="UnrealBloomPass.v"+h,u.texture.generateMipmaps=!1,this.renderTargetsVertical.push(u),r=Math.round(r/2),a=Math.round(a/2)}const o=eg;this.highPassUniforms=Zi.clone(o.uniforms),this.highPassUniforms.luminosityThreshold.value=i,this.highPassUniforms.smoothWidth.value=.01,this.materialHighPassFilter=new Ce({uniforms:this.highPassUniforms,vertexShader:o.vertexShader,fragmentShader:o.fragmentShader}),this.separableBlurMaterials=[];const l=[3,5,7,9,11];r=Math.round(this.resolution.x/2),a=Math.round(this.resolution.y/2);for(let h=0;h<this.nMips;h++)this.separableBlurMaterials.push(this._getSeparableBlurMaterial(l[h])),this.separableBlurMaterials[h].uniforms.invSize.value=new dt(1/r,1/a),r=Math.round(r/2),a=Math.round(a/2);this.compositeMaterial=this._getCompositeMaterial(this.nMips),this.compositeMaterial.uniforms.blurTexture1.value=this.renderTargetsVertical[0].texture,this.compositeMaterial.uniforms.blurTexture2.value=this.renderTargetsVertical[1].texture,this.compositeMaterial.uniforms.blurTexture3.value=this.renderTargetsVertical[2].texture,this.compositeMaterial.uniforms.blurTexture4.value=this.renderTargetsVertical[3].texture,this.compositeMaterial.uniforms.blurTexture5.value=this.renderTargetsVertical[4].texture,this.compositeMaterial.uniforms.bloomStrength.value=e,this.compositeMaterial.uniforms.bloomRadius.value=.1;const c=[1,.8,.6,.4,.2];this.compositeMaterial.uniforms.bloomFactors.value=c,this.bloomTintColors=[new P(1,1,1),new P(1,1,1),new P(1,1,1),new P(1,1,1),new P(1,1,1)],this.compositeMaterial.uniforms.bloomTintColors.value=this.bloomTintColors,this.copyUniforms=Zi.clone(Ns.uniforms),this.blendMaterial=new Ce({uniforms:this.copyUniforms,vertexShader:Ns.vertexShader,fragmentShader:Ns.fragmentShader,blending:Pr,depthTest:!1,depthWrite:!1,transparent:!0}),this._oldClearColor=new Ft,this._oldClearAlpha=1,this._basic=new qn,this._fsQuad=new Ga(null)}dispose(){for(let t=0;t<this.renderTargetsHorizontal.length;t++)this.renderTargetsHorizontal[t].dispose();for(let t=0;t<this.renderTargetsVertical.length;t++)this.renderTargetsVertical[t].dispose();this.renderTargetBright.dispose();for(let t=0;t<this.separableBlurMaterials.length;t++)this.separableBlurMaterials[t].dispose();this.compositeMaterial.dispose(),this.blendMaterial.dispose(),this._basic.dispose(),this._fsQuad.dispose()}setSize(t,e){let n=Math.round(t/2),i=Math.round(e/2);this.renderTargetBright.setSize(n,i);for(let r=0;r<this.nMips;r++)this.renderTargetsHorizontal[r].setSize(n,i),this.renderTargetsVertical[r].setSize(n,i),this.separableBlurMaterials[r].uniforms.invSize.value=new dt(1/n,1/i),n=Math.round(n/2),i=Math.round(i/2)}render(t,e,n,i,r){t.getClearColor(this._oldClearColor),this._oldClearAlpha=t.getClearAlpha();const a=t.autoClear;t.autoClear=!1,t.setClearColor(this.clearColor,0),r&&t.state.buffers.stencil.setTest(!1),this.renderToScreen&&(this._fsQuad.material=this._basic,this._basic.map=n.texture,t.setRenderTarget(null),t.clear(),this._fsQuad.render(t)),this.highPassUniforms.tDiffuse.value=n.texture,this.highPassUniforms.luminosityThreshold.value=this.threshold,this._fsQuad.material=this.materialHighPassFilter,t.setRenderTarget(this.renderTargetBright),t.clear(),this._fsQuad.render(t);let o=this.renderTargetBright;for(let l=0;l<this.nMips;l++)this._fsQuad.material=this.separableBlurMaterials[l],this.separableBlurMaterials[l].uniforms.colorTexture.value=o.texture,this.separableBlurMaterials[l].uniforms.direction.value=Ti.BlurDirectionX,t.setRenderTarget(this.renderTargetsHorizontal[l]),t.clear(),this._fsQuad.render(t),this.separableBlurMaterials[l].uniforms.colorTexture.value=this.renderTargetsHorizontal[l].texture,this.separableBlurMaterials[l].uniforms.direction.value=Ti.BlurDirectionY,t.setRenderTarget(this.renderTargetsVertical[l]),t.clear(),this._fsQuad.render(t),o=this.renderTargetsVertical[l];this._fsQuad.material=this.compositeMaterial,this.compositeMaterial.uniforms.bloomStrength.value=this.strength,this.compositeMaterial.uniforms.bloomRadius.value=this.radius,this.compositeMaterial.uniforms.bloomTintColors.value=this.bloomTintColors,t.setRenderTarget(this.renderTargetsHorizontal[0]),t.clear(),this._fsQuad.render(t),this._fsQuad.material=this.blendMaterial,this.copyUniforms.tDiffuse.value=this.renderTargetsHorizontal[0].texture,r&&t.state.buffers.stencil.setTest(!0),this.renderToScreen?(t.setRenderTarget(null),this._fsQuad.render(t)):(t.setRenderTarget(n),this._fsQuad.render(t)),t.setClearColor(this._oldClearColor,this._oldClearAlpha),t.autoClear=a}_getSeparableBlurMaterial(t){const e=[];for(let n=0;n<t;n++)e.push(.39894*Math.exp(-.5*n*n/(t*t))/t);return new Ce({defines:{KERNEL_RADIUS:t},uniforms:{colorTexture:{value:null},invSize:{value:new dt(.5,.5)},direction:{value:new dt(.5,.5)},gaussianCoefficients:{value:e}},vertexShader:`varying vec2 vUv;
				void main() {
					vUv = uv;
					gl_Position = projectionMatrix * modelViewMatrix * vec4( position, 1.0 );
				}`,fragmentShader:`#include <common>
				varying vec2 vUv;
				uniform sampler2D colorTexture;
				uniform vec2 invSize;
				uniform vec2 direction;
				uniform float gaussianCoefficients[KERNEL_RADIUS];

				void main() {
					float weightSum = gaussianCoefficients[0];
					vec3 diffuseSum = texture2D( colorTexture, vUv ).rgb * weightSum;
					for( int i = 1; i < KERNEL_RADIUS; i ++ ) {
						float x = float(i);
						float w = gaussianCoefficients[i];
						vec2 uvOffset = direction * invSize * x;
						vec3 sample1 = texture2D( colorTexture, vUv + uvOffset ).rgb;
						vec3 sample2 = texture2D( colorTexture, vUv - uvOffset ).rgb;
						diffuseSum += (sample1 + sample2) * w;
						weightSum += 2.0 * w;
					}
					gl_FragColor = vec4(diffuseSum/weightSum, 1.0);
				}`})}_getCompositeMaterial(t){return new Ce({defines:{NUM_MIPS:t},uniforms:{blurTexture1:{value:null},blurTexture2:{value:null},blurTexture3:{value:null},blurTexture4:{value:null},blurTexture5:{value:null},bloomStrength:{value:1},bloomFactors:{value:null},bloomTintColors:{value:null},bloomRadius:{value:0}},vertexShader:`varying vec2 vUv;
				void main() {
					vUv = uv;
					gl_Position = projectionMatrix * modelViewMatrix * vec4( position, 1.0 );
				}`,fragmentShader:`varying vec2 vUv;
				uniform sampler2D blurTexture1;
				uniform sampler2D blurTexture2;
				uniform sampler2D blurTexture3;
				uniform sampler2D blurTexture4;
				uniform sampler2D blurTexture5;
				uniform float bloomStrength;
				uniform float bloomRadius;
				uniform float bloomFactors[NUM_MIPS];
				uniform vec3 bloomTintColors[NUM_MIPS];

				float lerpBloomFactor(const in float factor) {
					float mirrorFactor = 1.2 - factor;
					return mix(factor, mirrorFactor, bloomRadius);
				}

				void main() {
					gl_FragColor = bloomStrength * ( lerpBloomFactor(bloomFactors[0]) * vec4(bloomTintColors[0], 1.0) * texture2D(blurTexture1, vUv) +
						lerpBloomFactor(bloomFactors[1]) * vec4(bloomTintColors[1], 1.0) * texture2D(blurTexture2, vUv) +
						lerpBloomFactor(bloomFactors[2]) * vec4(bloomTintColors[2], 1.0) * texture2D(blurTexture3, vUv) +
						lerpBloomFactor(bloomFactors[3]) * vec4(bloomTintColors[3], 1.0) * texture2D(blurTexture4, vUv) +
						lerpBloomFactor(bloomFactors[4]) * vec4(bloomTintColors[4], 1.0) * texture2D(blurTexture5, vUv) );
				}`})}}Ti.BlurDirectionX=new dt(1,0);Ti.BlurDirectionY=new dt(0,1);const Cs={name:"OutputShader",uniforms:{tDiffuse:{value:null},toneMappingExposure:{value:1}},vertexShader:`
		precision highp float;

		uniform mat4 modelViewMatrix;
		uniform mat4 projectionMatrix;

		attribute vec3 position;
		attribute vec2 uv;

		varying vec2 vUv;

		void main() {

			vUv = uv;
			gl_Position = projectionMatrix * modelViewMatrix * vec4( position, 1.0 );

		}`,fragmentShader:`

		precision highp float;

		uniform sampler2D tDiffuse;

		#include <tonemapping_pars_fragment>
		#include <colorspace_pars_fragment>

		varying vec2 vUv;

		void main() {

			gl_FragColor = texture2D( tDiffuse, vUv );

			// tone mapping

			#ifdef LINEAR_TONE_MAPPING

				gl_FragColor.rgb = LinearToneMapping( gl_FragColor.rgb );

			#elif defined( REINHARD_TONE_MAPPING )

				gl_FragColor.rgb = ReinhardToneMapping( gl_FragColor.rgb );

			#elif defined( CINEON_TONE_MAPPING )

				gl_FragColor.rgb = CineonToneMapping( gl_FragColor.rgb );

			#elif defined( ACES_FILMIC_TONE_MAPPING )

				gl_FragColor.rgb = ACESFilmicToneMapping( gl_FragColor.rgb );

			#elif defined( AGX_TONE_MAPPING )

				gl_FragColor.rgb = AgXToneMapping( gl_FragColor.rgb );

			#elif defined( NEUTRAL_TONE_MAPPING )

				gl_FragColor.rgb = NeutralToneMapping( gl_FragColor.rgb );

			#elif defined( CUSTOM_TONE_MAPPING )

				gl_FragColor.rgb = CustomToneMapping( gl_FragColor.rgb );

			#endif

			// color space

			#ifdef SRGB_TRANSFER

				gl_FragColor = sRGBTransferOETF( gl_FragColor );

			#endif

		}`};class ng extends Ci{constructor(){super(),this.uniforms=Zi.clone(Cs.uniforms),this.material=new kh({name:Cs.name,uniforms:this.uniforms,vertexShader:Cs.vertexShader,fragmentShader:Cs.fragmentShader}),this._fsQuad=new Ga(this.material),this._outputColorSpace=null,this._toneMapping=null}render(t,e,n){this.uniforms.tDiffuse.value=n.texture,this.uniforms.toneMappingExposure.value=t.toneMappingExposure,(this._outputColorSpace!==t.outputColorSpace||this._toneMapping!==t.toneMapping)&&(this._outputColorSpace=t.outputColorSpace,this._toneMapping=t.toneMapping,this.material.defines={},Gt.getTransfer(this._outputColorSpace)===jt&&(this.material.defines.SRGB_TRANSFER=""),this._toneMapping===_l?this.material.defines.LINEAR_TONE_MAPPING="":this._toneMapping===xl?this.material.defines.REINHARD_TONE_MAPPING="":this._toneMapping===vl?this.material.defines.CINEON_TONE_MAPPING="":this._toneMapping===Sa?this.material.defines.ACES_FILMIC_TONE_MAPPING="":this._toneMapping===Ml?this.material.defines.AGX_TONE_MAPPING="":this._toneMapping===Sl?this.material.defines.NEUTRAL_TONE_MAPPING="":this._toneMapping===yl&&(this.material.defines.CUSTOM_TONE_MAPPING=""),this.material.needsUpdate=!0),this.renderToScreen===!0?(t.setRenderTarget(null),this._fsQuad.render(t)):(t.setRenderTarget(e),this.clear&&t.clear(t.autoClearColor,t.autoClearDepth,t.autoClearStencil),this._fsQuad.render(t))}dispose(){this.material.dispose(),this._fsQuad.dispose()}}const Ar={primum:{x:0,z:-44,height:18,radius:10,buildings:24,accent:14988648},secundus:{x:-57,z:-9,height:13,radius:8,buildings:15,accent:13071958},tertius:{x:57,z:-9,height:20,radius:8,buildings:17,accent:8363651},quartus:{x:0,z:48,height:25,radius:11,buildings:27,accent:10386344},kibwe:{x:-39,z:26,height:11,radius:7,buildings:13,accent:6135448},elbrus:{x:39,z:26,height:23,radius:8,buildings:18,accent:7439790}};function ig(s){return()=>{s|=0,s=s+1831565813|0;let t=Math.imul(s^s>>>15,1|s);return t=t+Math.imul(t^t>>>7,61|t)^t,((t^t>>>14)>>>0)/4294967296}}function ye(s,t=.78,e=.12){return new Bi({color:s,roughness:t,metalness:e})}function ne(s,t,e,n=[0,0,0]){const i=new Me(s,t);return i.position.set(...n),i.castShadow=!0,i.receiveShadow=!0,e.add(i),i}class sg{constructor(t,e){this.canvas=t,this.game=e,this.random=ig(41723),this.clock=new ql,this.cityObjects=new Map,this.clickTargets=[],this.turbines=[],this.traffic=[],this.labels=[],this.pointerDown=new dt,this.tempVector=new P,this.raycaster=new jh,this.pointer=new dt,this.isMobile=matchMedia("(max-width: 700px)").matches,this.renderer=new Im({canvas:t,antialias:!0,alpha:!1,powerPreference:"high-performance"}),this.renderer.setPixelRatio(Math.min(devicePixelRatio,this.isMobile?1.35:1.8)),this.renderer.shadowMap.enabled=!this.isMobile,this.renderer.shadowMap.type=ml,this.renderer.toneMapping=Sa,this.renderer.toneMappingExposure=.92,this.renderer.outputColorSpace=Be,this.renderer.info.autoReset=!1,this.scene=new wh,this.scene.background=new Ft(6771037),this.scene.fog=new Ia(9271162,.0038),this.camera=new Gs(-75,75,55,-55,.1,500),this.camera.position.set(0,105,104),this.camera.lookAt(0,0,4),this.controls=new Nm(this.camera,t),this.controls.target.set(0,0,4),this.controls.enableDamping=!0,this.controls.dampingFactor=.06,this.controls.minPolarAngle=.22,this.controls.maxPolarAngle=1.12,this.controls.minZoom=.65,this.controls.maxZoom=2.2,this.controls.enablePan=!0,this.controls.mouseButtons={LEFT:xn.ROTATE,MIDDLE:xn.DOLLY,RIGHT:xn.PAN},this.controls.touches={ONE:Pn.ROTATE,TWO:Pn.DOLLY_PAN},this.createLights(),this.createAtmosphere(),Object.entries(Ar).forEach(([n,i],r)=>this.createCity(n,i,r)),this.createRoutes(),this.ship=this.createShip(),this.scene.add(this.ship),this.createTraffic(),this.createPostprocessing(),this.createLabels(),this.bindPicking(),this.resize(),window.addEventListener("resize",()=>this.resize())}createLights(){this.scene.add(new Wh(16767406,2630206,2.25)),this.sun=new Do(16762765,4.4),this.sun.position.set(-60,95,-35),this.sun.castShadow=!this.isMobile,this.sun.shadow.mapSize.set(2048,2048),Object.assign(this.sun.shadow.camera,{left:-100,right:100,top:100,bottom:-100,near:10,far:250}),this.sun.shadow.bias=-25e-5,this.scene.add(this.sun);const t=new Do(8494036,1.25);t.position.set(70,25,70),this.scene.add(t)}createAtmosphere(){const t=new Me(new Ji(600,600),new qn({color:3155008}));t.rotation.x=-Math.PI/2,t.position.y=-24,this.scene.add(t);const e=new Ba(1,1),n=new Bi({color:14272445,transparent:!0,opacity:.42,roughness:1,depthWrite:!1});this.clouds=new Rh(e,n,this.isMobile?100:180);const i=new fe;this.cloudData=[];for(let c=0;c<this.clouds.count;c++){const h=this.random()*Math.PI*2,d=24+this.random()*145,u={x:Math.cos(h)*d,z:Math.sin(h)*d,y:-14-this.random()*8,speed:.35+this.random()*.5,scale:[3+this.random()*5,.8+this.random()*1.2,2+this.random()*4]};this.cloudData.push(u),i.position.set(u.x,u.y,u.z),i.scale.set(...u.scale),i.rotation.y=this.random()*Math.PI,i.updateMatrix(),this.clouds.setMatrixAt(c,i.matrix)}this.scene.add(this.clouds);const r=280,a=new Float32Array(r*3);for(let c=0;c<r;c++)a[c*3]=(this.random()-.5)*400,a[c*3+1]=70+this.random()*100,a[c*3+2]=(this.random()-.5)*400;const o=new me;o.setAttribute("position",new Xe(a,3)),this.stars=new Uh(o,new Vl({color:16771797,size:.45,transparent:!0,opacity:.32,depthWrite:!1})),this.scene.add(this.stars),this.lightning=new Lo(12089599,0,130,1.5),this.lightning.position.set(25,-6,20),this.scene.add(this.lightning);const l=new Me(new Fa(13,48),new qn({color:16765596,fog:!1}));l.position.set(-105,45,-130),l.lookAt(this.camera.position),this.scene.add(l)}createCity(t,e,n){const i=new tn;i.position.set(e.x,0,e.z),this.scene.add(i);const r=ye(n%2?5984591:6707025,.98,.02),a=new Ge(e.radius*.82,1.2,e.height+18,9,5),o=a.attributes.position;for(let w=0;w<o.count;w++){const A=o.getY(w),C=(this.random()-.5)*1.25*(1-Math.abs(A)/(e.height+24));o.setX(w,o.getX(w)+C),o.setZ(w,o.getZ(w)+C)}a.computeVertexNormals(),ne(a,r,i,[0,-e.height/2-2,0]);const l=ne(new Ge(e.radius,e.radius*.86,1.4,12),ye(5790550,.95),i,[0,4.3,0]);l.receiveShadow=!0,ne(new Ge(e.radius*.78,e.radius*.84,.65,16),ye(7037526,.65,.35),i,[0,5.25,0]);const c=ne(new Gi(e.radius*.8,.18,6,32),ye(e.accent,.45,.65),i,[0,5.55,0]);c.rotation.x=Math.PI/2;const h=ye(n===1?9200716:7826271,.9,.06),d=ye(n%2?4352871:6049865,.65,.42),u=new Bi({color:16763255,emissive:16753723,emissiveIntensity:2.2,roughness:.4});for(let w=0;w<e.buildings;w++){const A=w*2.39996+n,C=Math.sqrt((w+1)/e.buildings)*e.radius*.64,D=Math.cos(A)*C,y=Math.sin(A)*C,x=1.1+this.random()*1.25,L=1.1+this.random()*1.15,N=1.8+this.random()*(t==="primum"||t==="quartus"?5.8:3.8);ne(new De(x,N,L),h,i,[D,5.6+N/2,y]);const k=ne(new Hs(Math.max(x,L)*.78,.75,4),d,i,[D,5.95+N,y]);k.rotation.y=Math.PI/4,w%2===0&&ne(new De(x*.55,.18,.06),u,i,[D,5.8+N*.63,y+L/2+.035]),w%5===0&&ne(new Ge(.15,.2,2.2,6),ye(5590085,.7,.5),i,[D+x*.3,6.2+N,y])}const p=t==="primum"?15:9+this.random()*4;ne(new Ge(1.35,1.8,p,8),h,i,[0,5.6+p/2,0]),ne(new Hs(1.75,2.6,8),d,i,[0,6.9+p,0]);const g=new Bi({color:e.accent,emissive:e.accent,emissiveIntensity:4});ne(new Vi(.32,10,8),g,i,[0,8.3+p,0]);const v=ne(new De(5.4,.5,3.5),ye(5855059,.72,.5),i,[e.radius*.78,5.1,0]);v.rotation.y=.06;for(const w of[-1,1])ne(new Gi(e.radius*.55,.16,5,20,Math.PI),ye(9137480,.48,.72),i,[0,2.2,w*e.radius*.45]).rotation.set(Math.PI/2,0,w*.5);const m=new tn;m.position.set(-e.radius*.65,9,0);const f=ne(new Ge(.12,.22,7,7),ye(6445389,.6,.55),m,[0,-3.5,0]);f.castShadow=!1;const E=new tn;E.rotation.y=Math.PI/2;for(let w=0;w<4;w++){const A=ne(new De(.28,3.8,.12),ye(12232573,.65,.3),E,[0,1.8,0]);A.rotation.z=w*Math.PI/2,A.position.set(-Math.sin(w*Math.PI/2)*1.8,Math.cos(w*Math.PI/2)*1.8,0)}m.add(E),i.add(m),this.turbines.push(E);const b=new Me(new za(e.radius*.95,e.radius*1.02,48),new qn({color:e.accent,transparent:!0,opacity:.8,side:sn,depthWrite:!1}));b.rotation.x=-Math.PI/2,b.position.y=6.2,b.visible=!1,i.add(b);const S=new Me(new Ge(e.radius*1.3,e.radius*1.3,30,12),new qn({visible:!1}));S.position.y=7,S.userData.cityId=t,i.add(S),this.clickTargets.push(S),this.cityObjects.set(t,{group:i,ring:b,style:e,beaconMat:g})}createRoutes(){const t=[["primum","secundus"],["primum","tertius"],["primum","kibwe"],["primum","elbrus"],["secundus","kibwe"],["tertius","elbrus"],["kibwe","quartus"],["elbrus","quartus"],["kibwe","elbrus"]];this.routeLines=[];for(const[e,n]of t){const i=Ar[e],r=Ar[n],a=new zh(new P(i.x,10,i.z),new P((i.x+r.x)/2,19,(i.z+r.z)/2),new P(r.x,10,r.z)),o=new me().setFromPoints(a.getPoints(36)),l=new Ih(o,new Gh({color:15126444,transparent:!0,opacity:.18,dashSize:1.3,gapSize:1.8,depthWrite:!1}));l.computeLineDistances(),this.scene.add(l),this.routeLines.push(l)}}createShip(){const t=new tn,e=ye(10188107,.42,.72),n=ye(4608602,.55,.58),i=ye(12957079,.95,.02),r=new Bi({color:7789032,emissive:3521237,emissiveIntensity:4,roughness:.25});ne(new Vi(1,24,14),i,t,[0,2.1,0]).scale.set(1.75,1.25,3.8);for(let c=-2;c<=2;c++){const h=ne(new Gi(Math.sqrt(Math.max(.2,1-c*c/13))*1.78,.045,5,24),e,t,[0,2.1,c*1.25]);h.scale.y=.72}const o=ne(new Na(.85,3.4,5,12),n,t,[0,-.2,0]);o.rotation.x=Math.PI/2,ne(new De(1.45,.8,1.35),e,t,[0,.65,.6]),ne(new De(1.15,.38,.05),r,t,[0,.75,1.3]);for(const c of[-1.6,1.6]){const h=ne(new Ge(.58,.58,.3,16),n,t,[c,-.5,-.5]),d=ne(new Ge(.45,.45,.06,16),r,t,[c,-.68,-.5]);h.rotation.y=d.rotation.y=0;const u=new tn;u.position.set(c,.1,-2.35);for(let p=0;p<3;p++){const g=ne(new De(.13,1.65,.08),e,u,[0,.75,0]);g.rotation.z=p*Math.PI*2/3,g.position.set(-Math.sin(p*Math.PI*2/3)*.75,Math.cos(p*Math.PI*2/3)*.75,0)}t.add(u),this.turbines.push(u)}for(let c=0;c<3;c++)ne(new De(.75,.6,.8),ye(7954502,.9),t,[-.85+c*.85,.55,-1.15]);const l=new Lo(5227511,3,11,2);return l.position.set(0,-1,0),t.add(l),t.scale.setScalar(.78),t}createTraffic(){for(let t=0;t<(this.isMobile?4:8);t++){const e=new tn;ne(new Vi(1,10,6),ye(t%2?7433058:5858922,.8,.25),e).scale.set(.55,.42,1.5),ne(new De(.5,.35,.8),ye(5916213,.8,.3),e,[0,-.5,0]),this.scene.add(e),this.traffic.push({vessel:e,radius:35+this.random()*60,speed:.025+this.random()*.04,phase:this.random()*Math.PI*2,height:8+this.random()*18})}}createPostprocessing(){if(this.composer=new Qm(this.renderer),this.composer.addPass(new tg(this.scene,this.camera)),!this.isMobile){const t=new Ti(new dt(innerWidth,innerHeight),.32,.5,.86);this.composer.addPass(t)}this.composer.addPass(new ng)}createLabels(){const t=document.getElementById("city-labels");t.innerHTML="";for(const[e,n]of this.cityObjects){const i=document.createElement("button");i.className="city-label",i.dataset.city=e,i.innerHTML=`<span class="city-label__dot"></span><span>${this.game.constructor,""}${this.gameCityName(e)}</span><small>УД ${n.style.height>20?5:n.style.height>15?3:4}</small>`,i.addEventListener("click",r=>{r.stopPropagation(),this.game.selectCity(e)}),t.appendChild(i),this.labels.push({id:e,element:i})}}gameCityName(t){return this.game?.cityNames?.[t]||{primum:"Примум",secundus:"Секунд",tertius:"Тертиус",quartus:"Квартус",kibwe:"Кибо",elbrus:"Эльбрус"}[t]}bindPicking(){this.canvas.addEventListener("pointerdown",t=>this.pointerDown.set(t.clientX,t.clientY)),this.canvas.addEventListener("pointerup",t=>{if(Math.hypot(t.clientX-this.pointerDown.x,t.clientY-this.pointerDown.y)>7)return;const e=this.canvas.getBoundingClientRect();this.pointer.set((t.clientX-e.left)/e.width*2-1,-(t.clientY-e.top)/e.height*2+1),this.raycaster.setFromCamera(this.pointer,this.camera);const n=this.raycaster.intersectObjects(this.clickTargets,!1)[0];n&&this.game.selectCity(n.object.userData.cityId)})}resize(){const t=this.canvas.parentElement.clientWidth,e=this.canvas.parentElement.clientHeight;if(!t||!e)return;const n=t/e,i=this.isMobile?142:132;this.camera.left=-i*n/2,this.camera.right=i*n/2,this.camera.top=i/2,this.camera.bottom=-i/2,this.camera.updateProjectionMatrix(),this.renderer.setSize(t,e,!1),this.composer.setSize(t,e)}focusCity(t){const e=this.cityObjects.get(t);e&&this.controls.target.set(e.style.x*.22,2,e.style.z*.22+4)}render(){const t=this.clock.getElapsedTime(),e=this.game.state,n=e.timePhase||0,i=n===3,r=i?1513517:n===1?7440532:n===2?8868954:6771037;this.scene.background.lerp(new Ft(r),.012),this.scene.fog.color.copy(this.scene.background),this.stars.material.opacity=Is.lerp(this.stars.material.opacity,i?.8:.12,.02),this.sun.intensity=Is.lerp(this.sun.intensity,i?.6:3.4,.015),this.lightning.intensity=Math.max(0,this.lightning.intensity*.86),this.random()<.0018&&(this.lightning.intensity=40),this.turbines.forEach((l,c)=>{l.rotation.z=t*(c<6?.7:5.5)}),this.traffic.forEach(l=>{const c=t*l.speed+l.phase;l.vessel.position.set(Math.cos(c)*l.radius,l.height+Math.sin(c*3)*.7,Math.sin(c)*l.radius),l.vessel.rotation.y=-c});for(const[l,c]of this.cityObjects)c.ring.visible=this.game.selectedCity===l||e.currentCity===l,c.ring.material.opacity=this.game.selectedCity===l?.95:.42,c.ring.scale.setScalar(1+Math.sin(t*2.4)*.025),c.beaconMat.emissiveIntensity=3.2+Math.sin(t*2+c.style.x)*1.3;let a;if(e.ship.traveling&&e.ship.startPos&&e.ship.endPos){const l=Is.clamp(e.ship.travelProgress/e.ship.travelDuration,0,1),c=l*l*(3-2*l),h=this.normalizedToWorld(e.ship.startPos),d=this.normalizedToWorld(e.ship.endPos);a=h.lerp(d,c),a.y=12+Math.sin(l*Math.PI)*12,this.ship.rotation.y=Math.atan2(d.x-h.x,d.z-h.z),this.ship.rotation.z=Math.sin(l*Math.PI)*-.05}else{const l=this.cityObjects.get(e.currentCity);a=new P(l.style.x+l.style.radius*.85,9,l.style.z),this.ship.rotation.z*=.92}a.y+=Math.sin(t*1.5)*.25,this.ship.position.lerp(a,e.ship.traveling?.22:.08);const o={dinghy:.78,hauler:.96,freighter:1.15,cargoliner:1.36}[e.shipType]||.78;this.ship.scale.lerp(new P(o,o,o),.04),this.controls.update(),this.updateLabels(),this.renderer.info.reset(),this.composer.render(),this.frames=(this.frames||0)+1,window.__worldStats={calls:this.renderer.info.render.calls,triangles:this.renderer.info.render.triangles,geometries:this.renderer.info.memory.geometries,textures:this.renderer.info.memory.textures}}normalizedToWorld(t){return new P((t.x-.5)*150,10,(t.y-.5)*110)}updateLabels(){const t=this.canvas.clientWidth,e=this.canvas.clientHeight;for(const n of this.labels){const i=this.cityObjects.get(n.id);this.tempVector.set(i.style.x,17+(n.id==="primum"?10:0),i.style.z).project(this.camera);const r=this.tempVector.z<1&&Math.abs(this.tempVector.x)<1.15&&Math.abs(this.tempVector.y)<1.15;n.element.style.display=r?"":"none",n.element.style.transform=`translate(-50%, -100%) translate(${(this.tempVector.x*.5+.5)*t}px, ${(-this.tempVector.y*.5+.5)*e}px)`,n.element.classList.toggle("is-current",this.game.state.currentCity===n.id),n.element.classList.toggle("is-selected",this.game.selectedCity===n.id)}}}class rg{constructor(){this.ctx=null,this.master=null,this.muted=localStorage.getItem("cloudtrader_muted")==="1"}start(){if(this.ctx){this.ctx.resume();return}const t=window.AudioContext||window.webkitAudioContext;if(!t)return;this.ctx=new t,this.master=this.ctx.createGain(),this.master.gain.value=this.muted?0:.16,this.master.connect(this.ctx.destination);const e=this.ctx.sampleRate*3,n=this.ctx.createBuffer(1,e,this.ctx.sampleRate),i=n.getChannelData(0);let r=0;for(let c=0;c<e;c++)r=r*.985+(Math.random()*2-1)*.015,i[c]=r;const a=this.ctx.createBufferSource();a.buffer=n,a.loop=!0;const o=this.ctx.createBiquadFilter();o.type="lowpass",o.frequency.value=620;const l=this.ctx.createGain();l.gain.value=.45,a.connect(o).connect(l).connect(this.master),a.start(),[55,82.41,110].forEach((c,h)=>{const d=this.ctx.createOscillator(),u=this.ctx.createGain();d.type=h===0?"sine":"triangle",d.frequency.value=c,u.gain.value=h===0?.055:.018,d.connect(u).connect(this.master),d.start()})}toggle(){return this.muted=!this.muted,localStorage.setItem("cloudtrader_muted",this.muted?"1":"0"),this.master&&this.ctx&&this.master.gain.setTargetAtTime(this.muted?0:.16,this.ctx.currentTime,.05),this.muted}cue(t="ui"){if(!this.ctx||this.muted)return;const e=this.ctx.currentTime,n=this.ctx.createOscillator(),i=this.ctx.createGain();n.type=t==="error"?"sawtooth":"sine";const r=t==="success"?520:t==="launch"?110:t==="error"?150:360;n.frequency.setValueAtTime(r,e),n.frequency.exponentialRampToValueAtTime(t==="launch"?260:r*1.35,e+.18),i.gain.setValueAtTime(.001,e),i.gain.exponentialRampToValueAtTime(.18,e+.015),i.gain.exponentialRampToValueAtTime(.001,e+.28),n.connect(i).connect(this.master),n.start(e),n.stop(e+.3)}}const Dt={primum:{name:"Примум",x:.5,y:.15,color:"#FFE082",distance:0,items:["mezium","antigrav","mag_core","latex_raw","mnp_dose","food_ration","food_braga","water_purified","spare_parts","hull_plate","wire_copper","generator_mag","rotor_assembly","compass","sol_tracker","armor_plate","medical_kit","stimulant","crop_harvest","grain","tools_wrench","tools_drill","rope","lantern","clothing_warm","book","encrypted_doc","jewelry","antique","fuel_cell","battery_antigrav","gas_filter","control_unit"],ships:["dinghy","hauler","freighter","cargoliner"]},secundus:{name:"Секунд",x:.15,y:.45,color:"#EF5350",distance:3,items:["latex_raw","food_braga","water_purified","tools_wrench","rope","lantern","crop_harvest","grain"],ships:["dinghy","hauler"]},tertius:{name:"Тертиус",x:.85,y:.45,color:"#66BB6A",distance:3,items:["antigrav","mnp_dose","errey_drug","medical_kit","stimulant","encrypted_doc","jewelry","antique","fuel_cell","battery_antigrav","control_unit"],ships:["dinghy","hauler","freighter"]},quartus:{name:"Квартус",x:.5,y:.85,color:"#AB47BC",distance:5,items:["mag_core","generator_mag","rotor_assembly","compass","sol_tracker","armor_plate","spare_parts","hull_plate"],ships:["hauler","freighter","cargoliner"]},kibwe:{name:"Кибо",x:.25,y:.65,color:"#26A69A",distance:4,items:["food_ration","water_purified","tools_drill","clothing_warm","book","fuel_cell","gas_filter"],ships:["dinghy","hauler"]},elbrus:{name:"Эльбрус",x:.75,y:.65,color:"#5C6BC0",distance:4,items:["mezium","antigrav","errey_drug","stimulant","encrypted_doc","jewelry","antique","battery_antigrav","control_unit","sol_tracker"],ships:["hauler","freighter","cargoliner"]}},ue={mezium:{name:"Мезий",basePrice:15,weight:12},antigrav:{name:"Антигравий",basePrice:80,weight:6},mag_core:{name:"МАГ-сердечник",basePrice:120,weight:8},latex_raw:{name:"Латекс-сырец",basePrice:8,weight:10},mnp_dose:{name:"МНП-препарат",basePrice:45,weight:2},errey_drug:{name:"Препарат Эррея",basePrice:200,weight:1},food_ration:{name:"Рацион",basePrice:10,weight:8},food_braga:{name:"Брага",basePrice:5,weight:6},water_purified:{name:"Вода",basePrice:6,weight:5},spare_parts:{name:"Запчасти",basePrice:30,weight:7},hull_plate:{name:"Обшивка",basePrice:40,weight:15},wire_copper:{name:"Провод медный",basePrice:15,weight:4},generator_mag:{name:"Генератор МАГ",basePrice:150,weight:20},rotor_assembly:{name:"Ротор",basePrice:90,weight:12},compass:{name:"ГРАДАР",basePrice:50,weight:2},sol_tracker:{name:"СОЛ-трекер",basePrice:100,weight:1},armor_plate:{name:"Бронепластина",basePrice:80,weight:18},medical_kit:{name:"Аптечка",basePrice:35,weight:3},stimulant:{name:"Стимулятор",basePrice:55,weight:1},crop_harvest:{name:"Урожай",basePrice:15,weight:12},grain:{name:"Зерно",basePrice:10,weight:10},tools_wrench:{name:"Гаечный ключ",basePrice:10,weight:3},tools_drill:{name:"Дрель",basePrice:25,weight:6},rope:{name:"Веревка",basePrice:5,weight:4},lantern:{name:"Фонарь",basePrice:15,weight:3},clothing_warm:{name:"Тёплая одежда",basePrice:20,weight:5},book:{name:"Книга",basePrice:30,weight:3},encrypted_doc:{name:"Шифровка",basePrice:80,weight:1},jewelry:{name:"Бижутерия",basePrice:40,weight:1},antique:{name:"Антиквариат",basePrice:100,weight:3},fuel_cell:{name:"Топливная ячейка",basePrice:50,weight:4},battery_antigrav:{name:"Антиграв. батарея",basePrice:70,weight:5},gas_filter:{name:"Газовый фильтр",basePrice:25,weight:4},control_unit:{name:"Блок управления",basePrice:80,weight:4}},ag=5e4,hl=.05,og=.3,lg=20,cg=.5,hg=.03,Qe={dinghy:{name:"Шлюпка",cargo:40,speed:1.4,price:0,color:"#78909C",slots:1},hauler:{name:"Хаулер",cargo:80,speed:1,price:2e3,color:"#66BB6A",slots:2},freighter:{name:"Фрахтовик",cargo:150,speed:.75,price:6e3,color:"#EF5350",slots:3},cargoliner:{name:"Карго-лайнер",cargo:300,speed:.5,price:15e3,color:"#AB47BC",slots:5}},nn={cargo_c:{name:"Груз. контейнер C",type:"cargo",tier:"C",cargoBonus:15,speedMod:.92,price:500},cargo_b:{name:"Груз. контейнер B",type:"cargo",tier:"B",cargoBonus:25,speedMod:.88,price:1200},cargo_a:{name:"Груз. контейнер A",type:"cargo",tier:"A",cargoBonus:40,speedMod:.85,price:3e3},cargo_s:{name:"Груз. контейнер S",type:"cargo",tier:"S",cargoBonus:60,speedMod:.8,price:8e3},speed_c:{name:"Ускоритель C",type:"speed",tier:"C",speedBonus:.2,cargoMod:5,price:500},speed_b:{name:"Ускоритель B",type:"speed",tier:"B",speedBonus:.35,cargoMod:10,price:1200},speed_a:{name:"Ускоритель A",type:"speed",tier:"A",speedBonus:.5,cargoMod:15,price:3e3},speed_s:{name:"Ускоритель S",type:"speed",tier:"S",speedBonus:.7,cargoMod:20,price:8e3},contraband_sol:{name:"ОБХОД СОЛ",type:"special",tier:"S",price:500,description:"Контрабандный сканер. Выделяет запрещённые товары красным на рынке. Риск ареста при продаже: 13%."}},Cr=["errey_drug","encrypted_doc","jewelry","antique","stimulant"],ul={Food:["food_ration","food_braga","water_purified","crop_harvest","grain"],Medical:["mnp_dose","errey_drug","medical_kit","stimulant"],Fuel:["mezium","fuel_cell"],Antigrav:["antigrav","battery_antigrav"],Meziy:["mag_core","generator_mag","control_unit"],Resources:["latex_raw","hull_plate","wire_copper","rope","clothing_warm"],Tech:["spare_parts","rotor_assembly","compass","sol_tracker","tools_wrench","tools_drill","lantern","gas_filter"],Equipment:["armor_plate","book","encrypted_doc","jewelry","antique"]};function ug(s){return`./icons/categories/Icon_Cat_${Object.keys(ul).find(e=>ul[e].includes(s))||"Resources"}.png`}function dl(s){return`<img class="item-icon" src="${ug(s)}" alt="" loading="lazy">`}function dg(s){return`./icons/ships/Icon_ShipClass_${s==="dinghy"?"Light":s==="hauler"?"Medium":s==="freighter"?"Heavy":"HeavyII"}.png`}const Yn={primum:{name:"Примум",maxLevel:10,baseIncome:1},secundus:{name:"Секунд",maxLevel:5,baseIncome:1},tertius:{name:"Тертиус",maxLevel:8,baseIncome:2},quartus:{name:"Квартус",maxLevel:7,baseIncome:3},kibwe:{name:"Кибо",maxLevel:5,baseIncome:1},elbrus:{name:"Эльбрус",maxLevel:10,baseIncome:2}},fl={0:{name:"Рассвет",skyTop:"#2D1B4E",skyBottom:"#FFB74D",uiBg:"rgba(45, 27, 78, 0.88)",textPrimary:"#FFF8E7",textSecondary:"#E0B4D4",cityGlow:"#FFD54F",cloudColor:"255, 183, 77",starAlpha:.3},1:{name:"День",skyTop:"#5C6BC0",skyBottom:"#FFE082",uiBg:"rgba(60, 50, 80, 0.85)",textPrimary:"#FFF8E7",textSecondary:"#B39DDB",cityGlow:"#FFD54F",cloudColor:"255, 248, 231",starAlpha:0},2:{name:"Закат",skyTop:"#1A237E",skyBottom:"#FF6F00",uiBg:"rgba(40, 25, 50, 0.90)",textPrimary:"#FFE0B2",textSecondary:"#CE93D8",cityGlow:"#FFAB91",cloudColor:"255, 111, 0",starAlpha:.2},3:{name:"Ночь",skyTop:"#0D0D1A",skyBottom:"#1A1A3E",uiBg:"rgba(15, 12, 30, 0.92)",textPrimary:"#E0E0E0",textSecondary:"#7986CB",cityGlow:"#7986CB",cloudColor:"30, 30, 60",starAlpha:.8}},fg=[{id:"price_surge",name:"Всплеск цен",description:"Торговцы скупают товары! Цены на продажу выросли на 30%.",duration:3,priceModifier:1.3,type:"price",effect:"up"},{id:"price_crash",name:"Обвал цен",description:"Рынок перенасыщен! Цены на покупку упали на 25%.",duration:3,priceModifier:.75,type:"price",effect:"down"},{id:"fuel_shortage",name:"Дефицит топлива",description:"Синтез-банки подорожали на 50%. Заправка обойдётся дороже.",duration:4,fuelCostModifier:1.5,type:"fuel"},{id:"repair_sale",name:"Скидки на ремонт",description:"Доки предлагают ремонт со скидкой 40%.",duration:3,repairCostModifier:.6,type:"repair"},{id:"storm_warning",name:"Шторм над Завесой",description:"Ядовитые молнии усилились. Некоторые маршруты заблокированы!",duration:4,blockedRoutes:["primum-secundus","quartus-kibwe"],type:"route_block"},{id:"city_quarantine",name:"Карантин",description:"Город закрыт на карантин. Посадка возможна только с повреждениями.",duration:3,blockedCity:"kibwe",forceLandDamage:15,type:"city_block"},{id:"customs_raid",name:"Рейд таможни",description:"Таможня усилила проверки. Контрабанда под запретом, но можно заплатить пошлину.",duration:3,bypassCost:100,type:"customs"},{id:"trader_convoy",name:"Караван торговцев",description:"Проходит караван! Цены стабильны, но спрос вырос на 20%. Караван задерживает вас на день.",duration:3,priceModifier:1.2,type:"price",effect:"up",skip:!0},{id:"engine_trouble",name:"Поломка двигателя",description:"Ваш корабль требует ремонта. Ремонт дороже на 80%.",duration:3,repairCostModifier:1.8,type:"repair"},{id:"smugglers_path",name:"Путь контрабандистов",description:"Открыт тайный путь! Можно пройти мимо блокпостов за взятку.",duration:2,bypassCost:50,type:"customs"}],gn=[{day:1,title:"Тренажёрный зал",text:"Вы пришли в тюремный спортзал. Здесь пахнет потом и ржавчиной. Барсук, местный авторитет, предлагает вам 'пожать железо' за его честь. Ваши мышцы ноют от антигравитационной болезни.",choices:[{text:"🏋️ Пожать штангу (риск травмы)",outcome:{days_lost:1,health:-10,money:0,death_chance:0},result:"Вы подняли 80 кг! Барсук уважительно кивает."},{text:"🤸 Отжаться от пола",outcome:{days_lost:1,health:5,money:0,death_chance:0},result:"50 отжиманий! Вы чувствуете себя сильнее."},{text:"😴 Поспать на скамье",outcome:{days_lost:1,health:15,money:0,death_chance:0},result:"Сон на жёсткой скамье освежил вас."}]},{day:2,title:"Шахматы с Барсуком",text:"Барсук достал самодельные шахматы из хлеба и крошек. Он жульничает, но вы не можете доказать. Вокруг собрались зеки, они делают ставки сигаретами.",choices:[{text:"♟️ Играть честно (проигрыш)",outcome:{days_lost:1,health:0,money:-20,death_chance:0},result:"Вы проиграли. Барсук забрал ваши сигареты."},{text:"🎭 Обвинить в жульничестве",outcome:{days_lost:2,health:-15,money:0,death_chance:5},result:"Барсук ударил вас. Вы проведёте лишний день в карцере."},{text:"🤝 Сдаться и стать 'падалой'",outcome:{days_lost:1,health:-5,money:10,death_chance:0},result:"Вы стали кем-то вроде слуги. Но зато есть еда."}]},{day:3,title:"Баня по праздникам",text:"Сегодня 'банный день'. Пар с крыс и мезия обжигает лёгкие. В тумане кто-то крадёт мыло. Вы стоите голышом, чувствуя себя уязвимым.",choices:[{text:"🧼 Искать своё мыло",outcome:{days_lost:1,health:-5,money:0,death_chance:0},result:"Вы нашли мыло, но получили пинок от охранника."},{text:"♨️ Париться до упаду",outcome:{days_lost:1,health:10,money:0,death_chance:0},result:"Жара ослабила ваши мышцы, но душа очистилась."},{text:"💧 Спрятаться в холодной воде",outcome:{days_lost:1,health:-10,money:0,death_chance:2},result:"Вы простудились. Но зато вас не трогали."}]},{day:4,title:"Работа на кухне",text:"Надзиратель велел вам чистить картошку для 500 зеков. Картошка странная, фиолетовая — с Завесы. Рядом работает старик, который утверждает, что был пилотом 'Интеграла'.",choices:[{text:"🥔 Чистить быстро (качество ↓)",outcome:{days_lost:1,health:0,money:15,death_chance:0},result:"Вы заработали 15 CR. Картошка мыта плохо, но кто заметит?"},{text:"🍳 Украсть еду для себя",outcome:{days_lost:2,health:5,money:0,death_chance:5},result:"Вас поймали. Лишний день карцера, но вы сыты."},{text:"👂 Слушать истории старика",outcome:{days_lost:1,health:0,money:0,death_chance:0},result:"Старик рассказал координаты тайного рынка. Полезно!"}]},{day:5,title:"Драка в камере",text:"В камере напряжение. Кто-то украл чей-то паёк. Вспыхнула драка. Вы зажаты в углу — двое зеков требуют 'отдать должок'. Ваши рёбра всё ещё болят после падения с баржи.",choices:[{text:"👊 Драться (риск травмы)",outcome:{days_lost:1,health:-20,money:0,death_chance:10},result:"Вы победили, но сломали палец. Уважение +10."},{text:"🛡️ Защищаться и отступить",outcome:{days_lost:1,health:-5,money:0,death_chance:0},result:"Вы увернулись, но получили пару синяков."},{text:"💰 Отдать деньги",outcome:{days_lost:1,health:0,money:-30,death_chance:0},result:"Вы отдали 30 CR. Мир в камере восстановлен."}]},{day:6,title:"Смена охраны",text:"Новый надзиратель — молодой парень, явно первый день. Он забыл заблокировать терминал. Вы видите, как он вводит код от склада: 1-2-3-4. Старый зек шепчет: 'Сваливай, пока не поздно'.",choices:[{text:"🏃 Сбежать через склад",outcome:{days_lost:0,health:0,money:0,death_chance:0},result:"ПОБЕГ УДАЛСЯ! Вы на свободе, но без вещей.",escape:!0},{text:"📦 Украсть что-то со склада",outcome:{days_lost:1,health:0,money:50,death_chance:15},result:"Вы украли запчасти на 50 CR, но чуть не попались."},{text:"🤐 Остаться и молчать",outcome:{days_lost:1,health:0,money:0,death_chance:0},result:"Благоразумие победило. Вы остались в тюрьме."}]},{day:7,title:"Бунт в столовой",text:"В меню снова 'брага-суррогат' и 'хлеб из опилок'. Кто-то кинул тарелку в надзирателя. Начался бунт! Зеки громят столовую. Вы можете присоединиться или спрятаться.",choices:[{text:"🔥 Жечь мебель (бунт)",outcome:{days_lost:3,health:-10,money:0,death_chance:20},result:"Вас жестоко избили дубинками. 3 дня карцера."},{text:"🍞 Украсть еду в суматохе",outcome:{days_lost:1,health:0,money:25,death_chance:5},result:"Вы набили карманы едой. Еды хватит надолго."},{text:"🙈 Спрятаться под столом",outcome:{days_lost:1,health:5,money:0,death_chance:0},result:"Вы переждали бунт под столом. Мудрое решение."}]},{day:8,title:"Медосмотр",text:"Тюремный врач, похожий на скелет в халате, осматривает вас. У вас кашель — наверное, лёгкие пострадали от Завесы. Он предлагает 'экспериментальное лекарство' сомнительного вида.",choices:[{text:"💊 Выпить лекарство",outcome:{days_lost:1,health:20,money:-10,death_chance:10},result:"Вы чувствуете себя на 20 лет моложе! Или это галлюцинация?"},{text:"🏃 Убежать от врача",outcome:{days_lost:1,health:-5,money:0,death_chance:0},result:"Охранник поймал вас. Удар дубинкой по спине."},{text:"🗣️ Жаловаться на боли",outcome:{days_lost:1,health:10,money:0,death_chance:0},result:"Вас положили в 'лазарет'. Там кормят лучше."}]},{day:9,title:"Одиночная камера",text:"За нарушение режима вас кинули в 'одиночку'. Тесная клетка, свет только из коридора. Стены покрыты надписями. Вы нашли царапину: 'Интеграл вернётся'. Крысы смотрят на вас с надеждой.",choices:[{text:"🧘 Медитировать (сохранение рассудка)",outcome:{days_lost:1,health:5,money:0,death_chance:0},result:"Вы обрели внутренний покой. Почти как в гхибли-храме."},{text:"🐀 Кормить крыс",outcome:{days_lost:1,health:-5,money:0,death_chance:0},result:"Крысы стали вашими друзьями. Они хотят вашу руку."},{text:"🔨 Соскоблить надпись",outcome:{days_lost:1,health:-10,money:0,death_chance:5},result:"Вы соскоблили 'Интеграл'. Стражник ударил вас за порчу имущества."}]},{day:10,title:"Суд и освобождение",text:"Ваш последний день. Судья в мантии из облачного шёлка смотрит на вас с высоты. 'Вы обвиняетесь в контрабанде мезия и антигравия. Но тюрьма вас исправила, верно?' Вы смотрите на свои дрожащие руки.",choices:[{text:"🙏 Покаяться и обещать исправиться",outcome:{days_lost:0,health:0,money:0,death_chance:0},result:"Судья смягчился. ВЫ СВОБОДНЫ! Но без имущества.",release:!0},{text:"💼 Сказать, что у вас есть деньги для взятки",outcome:{days_lost:0,health:0,money:-100,death_chance:0},result:"Судья взял взятку. Вы свободны и можете купить новый корабль!"},{text:"😤 Кричать о несправедливости",outcome:{days_lost:5,health:-20,money:0,death_chance:25},result:"Судья в ярости. 5 лет дополнительно! Вы умрёте здесь..."}]}],Rr=[{id:"dice_game",title:"Игра в кости",text:"За столиком в углу сидит человек в потёртой куртке. Он щелкает двумя костями по столу. 'Хочешь попробовать удачу, пилот? Ставка 50 CR. Может, сегодня твой день.'",choices:[{text:"🎲 Играть честно (ставка 50 CR)",outcome:{health:0,money:0},next:"dice_fair"},{text:"🕵️ Подглядеть его кости (риск разоблачения)",outcome:{health:0,money:0},next:"dice_cheat"},{text:"🚪 Отказаться и уйти",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"neutral",title:"УШЛИ",text:"Вы решаете не рисковать. Человек пожимает плечами."}}]},{id:"dice_fair",title:"Игра в кости — Честная игра",text:"Вы садитесь. Кости щелкают по столу. Ваши: [4, 3] = 7. Его: [5, 2] = 7. Ничья! 'Хорошая игра, пилот. Ещё раз?'",choices:[{text:"🎲 Ещё раунд (ставка 50 CR)",outcome:{health:0,money:0},next:"dice_fair2"},{text:"📋 Повысить ставку до 100 CR",outcome:{health:0,money:0},next:"dice_high"},{text:"🚪 Выйти из игры",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"neutral",title:"КОНЕЦ ИГРЫ",text:"Вы уходите с пустыми руками, но и без потерь."}}]},{id:"dice_fair2",title:"Игра в кости — Второй раунд",text:"Новое броскание. Ваши кости: [2, 6] = 8. Его кости: [3, 6] = 9. Вы проиграли 50 CR. 'Ха! Сегодня не твой день, пилот.'",choices:[{text:"😤 Потребовать реванш на 100 CR",outcome:{health:0,money:-100},next:"dice_revenge"},{text:"😓 Смириться и уйти",outcome:{health:0,money:-50},next:null,exit:!0,result:{type:"failure",title:"ПРОИГРЫШ",text:"Вы потеряли 50 CR. Человек ухмыляется."}}]},{id:"dice_high",title:"Игра в кости — Высокие ставки",text:"Ставки повышены до 100 CR! Базовый инстинкт кричит 'играй', но здравый смысл советует уйти. 'Ну что, пилот? Бросаем?'",choices:[{text:"🎲 Бросить кости",outcome:{health:0,money:0},next:"dice_high_result"},{text:"🚪 Передумать и уйти (-50 CR)",outcome:{health:0,money:-50},next:null,exit:!0,result:{type:"failure",title:"ОТСТУПИЛИ",text:"Вы теряете 50 CR за трусость и уходите."}}]},{id:"dice_high_result",title:"Игра в кости — Решающий бросок",text:"Кости взлетают в воздух... Вы卖了 все. Ваши: [6, 6] = 12! Критический успех! Человек бледнеет: 'Как... Ты мухлевал!'",choices:[{text:"😈 Забрать выигрыш (200 CR)",outcome:{health:0,money:200},next:null,exit:!0,result:{type:"success",title:"ПОБЕДА!",text:"Вы уходите с 200 CR. Человек смотрит вам в спину с ненавистью."}}]},{id:"dice_cheat",title:"Игра в кости — Попытка мухлежа",text:"Вы пытаетесь подсмотреть его кости в отражении стакана. Но человек заметил! 'Эй! Ты что делаешь, пилот?!'",choices:[{text:"🤥 Притвориться пьяным",outcome:{health:0,money:0},next:"dice_cheat_deny"},{text:"🏃 Бежать со стулом",outcome:{health:-10,money:0},next:null,exit:!0,result:{type:"failure",title:"ПОБЕГ",text:"Вы опрокинули стул и сбежали. Человек кричит вдогонку. -10 HP от удара об угол."}}]},{id:"dice_cheat_deny",title:"Игра в кости — Отрицание",text:"'Я... просто смотрел на стену за тобой, приятель. Ты что, параноик?' Человек щурится, но его друзья смеются: 'Да ладно, отпусти его, Грон.' Он неохотно кивает.",choices:[{text:"🍺 Предложить угостить его пивом",outcome:{health:0,money:-20},next:null,exit:!0,result:{type:"neutral",title:"МИр",text:"Вы помирились. Грон рассказал, что на Кибо можно купить контрабанду втридорога."}},{text:"🚶 Просто уйти",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"neutral",title:"УШЛИ",text:"Вы решаете не испытывать удачу. Человек качает головой."}}]},{id:"dice_revenge",title:"Игра в кости — Реванш",text:"'Хочешь отыграться? Ну давай!' Вы бросаете все в бой. Ваши кости: [1, 1] = 2. Его кости: [4, 4] = 8. Разгром! Ваши 100 CR ушли в его карман.",choices:[{text:"😡 Обвинить в жульничестве",outcome:{health:-15,money:0},next:null,exit:!0,result:{type:"failure",title:"ПОБОЙ",text:"Грон ударил вас в лицо. '-100 CR и фонарь под глазом. Не обвиняй честных игроков, пилот.'"}},{text:"😭 Уйти с оставшимся достоинством",outcome:{health:0,money:-100},next:null,exit:!0,result:{type:"failure",title:"ПОТЕРЯ",text:"Вы уходите с пустым кошельком. Грон машет вам рукой: 'Возвращайся, когда будут деньги!'"}}]},{id:"drunk_bartender",title:"Пьяный бармен",text:"Бармен, на вид ветеран Завесы, наливает себе браги прямо из бочки. 'Садись, пилот. Расскажу кое-что... даром.' Он хихикает. 'Ты знаешь, что на Секунде цены на латекс падают каждую вторую луну?'",choices:[{text:"🍺 Выпить с барменом",outcome:{health:-5,money:0},next:"drunk_bartender_drink"},{text:"👂 Слушать внимательно",outcome:{health:0,money:0},next:"drunk_bartender_info"},{text:"🚶 Уйти",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"neutral",title:"УШЛИ",text:"Бармен пожимает плечами и допивает свою брагу."}}]},{id:"drunk_bartender_drink",title:"Пьяный бармен — Угощение",text:"Бармен наливает вам стакана три 'Облачной браги'. Мир плывёт перед глазами. 'Так вот, пилот... Третий город, Квартус... Там делают лучшие моторы. Но у них вечно не хватает маг-сердечников.'",choices:[{text:"😵 Поблагодарить и уйти (опьянение)",outcome:{health:-15,money:0},next:null,exit:!0,result:{type:"neutral",title:"ОПЬЯНЕНИЕ",text:"Вы выходите, покачиваясь. На стене написано: 'Маг-сердечники = прибыль на Квартусе'."}}]},{id:"drunk_bartender_info",title:"Пьяный бармен — Информация",text:"'Слушай сюда!' Бармен понижает голос. 'Торговец по имени Рашид на Эльбрусе... Он покупает украшения за двойную цену. Но только если они 'с историей'. Понимаешь?' Он подмигивает.",choices:[{text:"👍 Понял, спасибо!",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"success",title:"ПОДСКАЗКА",text:"Бармен кивает и возвращается к своей браге. Вы узнали: на Эльбрусе украшения стоят дорого!"}}]},{id:"bar_fight",title:"Драка в баре",text:"Какой-то здоровяк в кожаной куртке задевает вас плечом. 'Эй ты, мелкий торгаш! Ты на моей летательной траектории!' Его кулачище размером с вашу голову.",choices:[{text:"👊 Вмазать ему первому",outcome:{health:-20,money:0},next:"bar_fight_start"},{text:"😰 Попытаться договориться",outcome:{health:0,money:-30},next:null,exit:!0,result:{type:"neutral",title:"МШАНИЕ",text:"'Вот 30 CR, приятель. Купи себе пива.' Здоровяк хмыкает и отступает."}},{text:"🏃 Отступить к выходу",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"neutral",title:"ОТСТУПЛЕНИЕ",text:"Вы решаете не связываться. Здоровяк смеётся вам вслед."}}]},{id:"bar_fight_start",title:"Драка — Начало",text:"Вы бьёте первым! Удар попадает ему в челюсть. Но он даже не шатается. 'Хороший удар, мелкий. Дай-ка я тебе отвечу...'",choices:[{text:"🛡️ Защищаться (блок)",outcome:{health:-10,money:0},next:"bar_fight_block"},{text:"⚡ Атаковать снова!",outcome:{health:-25,money:0},next:"bar_fight_attack"},{text:"🏃 Бежать!",outcome:{health:-5,money:0},next:null,exit:!0,result:{type:"failure",title:"ПОБЕГ",text:"Вы бежите, но он догоняет и бьёт в спину. -5 HP."}}]},{id:"bar_fight_block",title:"Драка — Блок",text:"Вы подставляете руку. Его удар обрушивается на ваш предплечье. Больно, но вы устояли! 'Ещё раунд?'",choices:[{text:"👊 Финальный удар!",outcome:{health:-15,money:50},next:null,exit:!0,result:{type:"success",title:"ПОБЕДА!",text:"Вы делаете подсечку! Здоровяк падает. Бармен выдаёт вам 50 CR: 'За зрелищность!'"}},{text:"🤝 Протянуть руку",outcome:{health:-10,money:0},next:null,exit:!0,result:{type:"neutral",title:"МИР",text:"'Неплохо, мелкий.' Он пожимает вам руку. Вы подружились. '-10 HP, но без денежных потерь.'"}}]},{id:"bar_fight_attack",title:"Драка — Атака",text:"Вы бросаетесь в атаку! Но он ловит вас за горло и поднимает в воздух. 'Ты что, самоубийца?'",choices:[{text:"😵 Сдаться",outcome:{health:-25,money:0},next:null,exit:!0,result:{type:"failure",title:"ПОРАЖЕНИЕ",text:"'Умный выбор.' Он бросает вас на пол. '-25 HP, но выжили.'"}},{text:"🦵 Ударить ногой в пах",outcome:{health:-15,money:100},next:null,exit:!0,result:{type:"success",title:"ПОБЕДА!",text:"Он согнулся пополам. Бармен даёт вам 100 CR: 'Грязный приём, но рабочий!'"}}]},{id:"black_market",title:"Странный торговец",text:"К вам подходит человек в капюшоне. 'Тсс... Ты ведь торгуешь, пилот? У меня есть кое-что для настоящих дельцов. Модуль 'Туман' называется. Позволяет проходить таможню без осмотра. 300 CR. Гарантия на слово.'",choices:[{text:"💰 Купить модуль",outcome:{health:0,money:-300},next:"blackmarket_buy"},{text:"🔍 Потребовать показать товар",outcome:{health:0,money:0},next:"blackmarket_check"},{text:"🚓 Вызвать охрану",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"neutral",title:"ОТКАЗ",text:"'Охрана? Ха! Ты ещё об этом пожалеешь, пилот.' Человек исчезает в толпе."}}]},{id:"blackmarket_buy",title:"Странный торговец — Покупка",text:"Вы отдаёте 300 CR. Торговец вкладывает вам в руку маленькую коробку. 'Установи на корабль. И помни: никто не должен знать, откуда это.'",choices:[{text:"👍 Принять",outcome:{health:0,money:-300},next:null,exit:!0,result:{type:"success",title:"ПРИОБРЕТЕНО",text:"Вы получили 'Туман' (модуль контрабанды)! +13% шанс избежать ареста."}}]},{id:"blackmarket_check",title:"Странный торговец — Проверка",text:"'Товар покажешь? А то мало ли...' Торговец оскаливается: 'Ты что, не доверяешь Рашиду? Ну и иди к чертям!'",choices:[{text:"😠 Извиниться и купить",outcome:{health:0,money:-300},next:"blackmarket_buy"},{text:"🚶 Уйти",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"neutral",title:"УШЛИ",text:"'Рашид' исчезает. Вы так и не узнали, был ли это настоящий модуль."}}]},{id:"old_pilot",title:"Старый пилот",text:"За стойкой сидит седой человек с протезом руки. Когда-то он вёл грузовой корабль. 'Сядешь, пилот? Помню твой тип... Такие раньше вёз мне 'Интеграл'.'",choices:[{text:"🍺 Угостить ветерана",outcome:{health:0,money:-15},next:"oldpilot_stories"},{text:"👂 Спросить о Завесе",outcome:{health:0,money:0},next:"oldpilot_veil"},{text:"🚶 Уйти",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"neutral",title:"УШЛИ",text:"Ветеран качает головой: 'Молодёжь...'"}}]},{id:"oldpilot_stories",title:"Старый пилот — Истории",text:"'Хороший из тебя пилот, коли угощаешь.' Он смеётся. 'Знаешь, где я спрятал свою пенсию? В заброшенном доке на Кибо. Там ящик с маг-сердечниками. Забирай, если найдёшь.'",choices:[{text:"🙏 Спасибо за наводку!",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"success",title:"ПОДСКАЗКА",text:"Старый пилот дал вам координаты тайника на Кибо! (+маг-сердечники)"}}]},{id:"oldpilot_veil",title:"Старый пилот — Завеса",text:"'Завеса? Опасное место. Но я там бывал.' Он понижает голос. 'Один совет: никогда не верь торговцам на Тертиусе. Они продают воду как антигравий, если понимаешь о чём я.'",choices:[{text:"👍 Запомню",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"success",title:"ПОДСКАЗКА",text:"Вы узнали: на Тертиусе разводняют антигравий! Проверяйте товар перед покупкой."}}]},{id:"manager_job",title:"Бар-менеджер",text:"Хозяин бара, важный человек в костюме, подходит к вам. 'Пилот! Слышал, ты надёжный. Мне нужна доставка: 5 единиц медикаментов в Квартус. Заплачу 200 CR. Но если попадёшься с этим на таможне — я знать тебя не знаю.'",choices:[{text:"📦 Взять контракт",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"success",title:"КОНТРАКТ",text:"Вы приняли контракт: доставить медикаменты в Квартус. Внимание: это контрабанда!"}},{text:"🤔 Потребовать больше",outcome:{health:0,money:0},next:"manager_negotiate"},{text:"🚫 Отказаться",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"neutral",title:"ОТКАЗ",text:"'Ну и ладно. Найду другого дурака.' Менеджер уходит."}}]},{id:"manager_negotiate",title:"Бар-менеджер — Торг",text:"'Больше? Ха! Хорошо, 250 CR. Но медикаменты ты берёшь у меня. Это тебе скидка.'",choices:[{text:"👍 Договорились!",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"success",title:"КОНТРАКТ",text:"Вы приняли контракт: доставить медикаменты в Квартус за 250 CR."}},{text:"🚪 Выйти",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"neutral",title:"УШЛИ",text:"Вы уходите. Менеджер пожимает плечами."}}]},{id:"thugs",title:"Хулиганы",text:"Трое здоровых парней围着 ваш столик. 'Деньги, пилот. Или мы тебя из окна.' Бармен делает вид, что не замечает. Похоже, никто вам не поможет.",choices:[{text:"💵 Отдать деньги (50 CR)",outcome:{health:0,money:-50},next:null,exit:!0,result:{type:"failure",title:"ОГРАБЛЕНИЕ",text:"'Умный выбор.' Они забирают 50 CR и уходят."}},{text:"🍺 Бросить стакан в главаря",outcome:{health:-15,money:0},next:"thugs_fight"},{text:"🗣️ Попытаться договориться",outcome:{health:0,money:-20},next:null,exit:!0,result:{type:"neutral",title:"СДЕЛКА",text:"'Ладно, 20 CR и мы в расчёте.' Вы платите и решаете уйти."}}]},{id:"thugs_fight",title:"Хулиганы — Битва",text:"Стакан разбивается о голову главаря. Он взвыл! 'Убейте его!' Двое бросаются на вас. Но бармен неожиданно грохает об стену одного из них: 'Никакого насилия в моём баре!'",choices:[{text:"🏆 Помочь бармену",outcome:{health:-10,money:30},next:null,exit:!0,result:{type:"success",title:"ПОБЕДА!",text:"'Спасибо, приятель!' Бармен даёт вам 30 CR и бесплатную выпивку."}},{text:"🏃 Бежать!",outcome:{health:-5,money:0},next:null,exit:!0,result:{type:"failure",title:"ПОБЕГ",text:"Вы бежите, но один успевает ударить вас в спину. '-5 HP.'"}}]},{id:"musician",title:"Музыкант",text:"В углу бара сидит слепой музыкант с гитарой. Он играет старую песню про 'Интеграл'. В баре становится тихо. 'Это для тебя, пилот.'",choices:[{text:"💰 Бросить монету (10 CR)",outcome:{health:0,money:-10},next:"musician_tip"},{text:"🎵 Попросить другую песню",outcome:{health:0,money:0},next:"musician_song"},{text:"🚶 Просто уйти",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"neutral",title:"УШЛИ",text:"Музыкант продолжает играть, но вы уходите."}}]},{id:"musician_tip",title:"Музыкант — Чаевые",text:"'Спасибо, добрый пилот!' Музыкант улыбается. 'Знаешь, что самое ценное в этом мире? Не деньги. Информация. А она часто прячется в книгах.'",choices:[{text:"👂 Продолжать слушать",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"success",title:"ПОДСКАЗКА",text:"'Книги на Кибо стоят дёшево. Но те, что с Эльбруса — там секреты торговцев.' Вы узнали что-то полезное."}}]},{id:"musician_song",title:"Музыкант — Другая песня",text:"'Хочешь другую? Могу спеть про любовь, про войну... или про то, как один пилот обманул всю таможню Завесы.'",choices:[{text:"🎶 Спой про таможню",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"success",title:"СЕКРЕТ",text:"'Жил пилот хитрый, знал он путь — где туман гуще, там заблудишься...'Музыкант поёт о тайных маршрутах. Вы узнали о 'Туманном пути' через Эльбрус."}},{text:"💔 Спой про любовь",outcome:{health:5,money:0},next:null,exit:!0,result:{type:"neutral",title:"БАЛЛАДА",text:"'Любовь прошла, как облако, — и ты остался один...' От этой песни стало тепло на душе. +5 HP."}}]},{id:"missing_package",title:"Пропавшая посылка",text:"К вам подбегает женщина в слезах. 'Вы пилот! Вы должны мне помочь! Моя посылка с украшениями пропала! Говорят, это вы её украли!' Охрана бара смотрит в вашу сторону.",choices:[{text:"😠 Это не я! Давайте разберёмся",outcome:{health:0,money:0},next:"package_innocent"},{text:"💰 Предложить 50 CR за молчание",outcome:{health:0,money:-50},next:null,exit:!0,result:{type:"neutral",title:"ВЗЯТКА",text:"'Думаешь, деньги решат всё?' Но она берёт 50 CR и уходит. Вы вне подозрений."}},{text:"🏃 Бежать!",outcome:{health:-10,money:0},next:null,exit:!0,result:{type:"failure",title:"ПОБЕГ",text:"Охрана ловит вас и избивает. '-10 HP. Вас все ещё подозревают.'"}}]},{id:"package_innocent",title:"Пропавшая посылка — Невиновность",text:"'Я не брал никаких посылок! Давайте проверим мои вещи.' Охрана обыскивает вас — ничего нет. Женщина бледнеет: 'Тогда кто...? Ой...'",choices:[{text:"👀 Кто-то другой!",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"success",title:"НЕВИНОВЕН",text:"'Это был... нет, не скажу.' Женщина убегает. Охрана извиняется. Вы стали популярнее в баре за честность."}},{text:"🔍 Помочь найти виновного",outcome:{health:0,money:75},next:null,exit:!0,result:{type:"success",title:"РАССЛЕДОВАНИЕ",text:"'Я видел, как кто-то в сером выходил через заднюю дверь...' Охрана ловит вора. Вам дают 75 CR за помощь."}}]},{id:"drunk_arguments",title:"Пьяные споры",text:"За соседним столиком двое пьяных торговцев громко спорят. 'На Эльбрусе цены на украшения упадут через неделю!' — 'Глупости! Рашид скупает всё подряд!'",choices:[{text:"👂 Внимательно слушать",outcome:{health:0,money:0},next:"drunk_listen"},{text:"🍺 Присоединиться к спору",outcome:{health:-5,money:0},next:"drunk_join"},{text:"🚶 Игнорировать",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"neutral",title:"УШЛИ",text:"Вы не обращаете внимания на пьяниц. Напрасно — информация была ценной."}}]},{id:"drunk_listen",title:"Пьяные споры — Подслушанное",text:"Один торговец спотыкается и падает. Другой смеётся: 'Рашид — жмот! Я сам продал ему украшений на 500 CR по дешёвке!' Вы усвоили: на Эльбрусе Рашид — ключевой покупатель.",choices:[{text:"👍 Полезно",outcome:{health:0,money:0},next:null,exit:!0,result:{type:"success",title:"ПОДСКАЗКА",text:"Вы узнали: Рашид на Эльбрусе скупает украшения оптом. Прибыльная цель!"}}]},{id:"drunk_join",title:"Пьяные споры — Участие",text:"'Эй, вы чушь несёте!' Оба торговца поворачиваются к вам. 'А ты откуда знаешь, мелкий?' Начинается спор... который переходит в драку.",choices:[{text:"🍻 Выпить с ними и помириться",outcome:{health:-10,money:-20},next:null,exit:!0,result:{type:"neutral",title:"МИР",text:"'Ладно, милый пилот, садись!' Вы пьёте вместе. Они раскрывают секрет: 'Квартус закупает запчасти у всех, но платит в 2 раза больше.' '-20 CR и -10 HP, но ценная информация.'"}},{text:"🏃 Уйти пока цел",outcome:{health:-5,money:0},next:null,exit:!0,result:{type:"failure",title:"ПОБЕГ",text:"Вы уходите, но один успел бросить в вас кружкой. '-5 HP.'"}}]}];class pg{constructor(){this.reset()}reset(){this.credits=800,this.currentCity="primum",this.cargo={},this.markets={},this.tick=0,this.timePhase=0,this.shipCondition=100,this.shipFuel=100,this.shipType="dinghy",this.ship={docked:!0,traveling:!1,destination:null,travelProgress:0,travelDuration:0,broken:!1},this.contracts=[],this.shipModules=[],this.activeEvents=[],this.eventLog=[],this.portLevels={};for(let t in Yn)this.portLevels[t]=0;this.pirateCombat={active:!1,playerHP:3,enemies:[],bullets:[],score:0,keys:{},mouse:{x:0,y:0,pressed:!1},touch:{active:!1,x:0,y:0,shooting:!1},playerX:400,playerY:300,playerAngle:0,lastShot:0,enemyBullets:[],nearestCity:null},this.initMarkets()}initMarkets(){for(let t in Dt){this.markets[t]={};for(let e in ue)this.markets[t][e]={demandFactor:.1+Math.random()*.3,supplyFactor:Math.random()*.3}}}getTotalCargoWeight(){let t=0;for(let e in this.cargo)t+=(this.cargo[e]||0)*ue[e].weight;return t}getTotalCargoBonus(){let t=0;for(let e of this.shipModules)nn[e]&&(t+=nn[e].cargoBonus||0);return t}getTotalSpeedBonus(){let t=0;for(let e of this.shipModules)nn[e]&&(t+=nn[e].speedBonus||0);return t}getExtraCargoPenalty(){let t=0;for(let e of this.shipModules)nn[e]&&(t+=nn[e].cargoMod||0);return t}getEffectiveCargo(){return Qe[this.shipType].cargo+this.getTotalCargoBonus()}getEffectiveSpeed(){return Qe[this.shipType].speed*(1+this.getTotalSpeedBonus())}getEffectiveCargoLimit(){return this.getEffectiveCargo()+this.getExtraCargoPenalty()}getAvailableSlots(){return Qe[this.shipType].slots}getUsedSlots(){return this.shipModules.length}canAddModule(){return this.getUsedSlots()<this.getAvailableSlots()}calculatePrice(t,e){let n=ue[e];if(!n||!this.markets[t]||!this.markets[t][e])return n?n.basePrice:0;let i=this.markets[t][e],r=n.basePrice*(1+i.demandFactor-i.supplyFactor);return r*=this.getPriceModifier(),Math.max(1,Math.round(r))}hasContrabandModule(){return this.shipModules.includes("contraband_sol")}hasActiveEvent(t){return this.activeEvents.some(e=>e.id===t)}isCityBlocked(t){return this.activeEvents.some(e=>e.type==="city_block"&&e.blockedCity===t)}isRouteBlocked(t){return this.activeEvents.some(e=>e.type==="route_block"&&e.blockedRoutes&&e.blockedRoutes.includes(t))}getPriceModifier(){let t=1;for(let e of this.activeEvents)e.priceModifier&&(e.effect==="up"?t=Math.max(t,e.priceModifier):e.effect==="down"?t=Math.min(t,e.priceModifier):t*=e.priceModifier);return t}getRepairCostModifier(){let t=1;for(let e of this.activeEvents)e.repairCostModifier&&(t*=e.repairCostModifier);return t}getFuelCostModifier(){let t=1;for(let e of this.activeEvents)e.fuelCostModifier&&(t*=e.fuelCostModifier);return t}getPortLevel(t){return this.portLevels[t]||0}getPortMaxLevel(t){return Yn[t]?Yn[t].maxLevel:0}getPortUpgradeCost(t){let e=this.getPortLevel(t);return(e+1)*(e+1)*500}getPortFuelDiscount(t){return this.getPortLevel(t)*.1}getPortPassiveIncome(t){let e=this.getPortLevel(t);return e*(e+1)/2}getTotalPassiveIncome(){let t=0;for(let e in this.portLevels)t+=this.getPortPassiveIncome(e);return t}addEventToLog(t,e){this.eventLog.unshift({tick:this.tick,eventName:t.name,action:e,description:t.description}),this.eventLog.length>10&&this.eventLog.pop()}save(){let t={credits:this.credits,currentCity:this.currentCity,cargo:this.cargo,markets:this.markets,tick:this.tick,timePhase:this.timePhase,shipCondition:this.shipCondition,shipFuel:this.shipFuel,ship:this.ship,contracts:this.contracts,shipType:this.shipType,shipModules:this.shipModules};localStorage.setItem("cloudtrader_save",JSON.stringify(t))}load(){let t=localStorage.getItem("cloudtrader_save");if(t){let e=JSON.parse(t);return Object.assign(this,e),this.shipModules=this.shipModules||[],(!this.markets||Object.keys(this.markets).length===0)&&this.initMarkets(),this.contracts||(this.contracts=[]),this.tick===void 0&&(this.tick=0),this.timePhase===void 0&&(this.timePhase=0),this.shipCondition===void 0&&(this.shipCondition=100),this.ship||(this.ship={docked:!0,traveling:!1,destination:null,travelProgress:0,travelDuration:0,broken:!1}),this.shipFuel===void 0&&(this.shipFuel=100),this.shipType||(this.shipType="dinghy"),!0}return!1}}class mg{constructor(){this.state=new pg,this.cityNames=Object.fromEntries(Object.entries(Dt).map(([e,n])=>[e,n.name])),this.sound=new rg,this.paused=!0,this.lastFrame=performance.now(),this.uiClock=0,this.canvas=document.getElementById("map-canvas"),this.selectedCity=null,this.marketTab="buy",this.qtyValues={},this.qtyMax={},this.setupCanvas(),this.bindEvents();let t=this.state.load();t||(this.generateContract(),this.generateContract()),t&&this.state.contracts.length===0&&this.generateContract(),this.setupClouds(),this.initStars(),this.updateTimePhaseCSS(),this.renderAll(),this.gameLoop()}setupCanvas(){this.world=new sg(this.canvas,this)}setupClouds(){}bindEvents(){this.bindShellEvents(),this.setupMobileCityList(),document.querySelectorAll(".market-tab").forEach(t=>{t.addEventListener("click",()=>{document.querySelectorAll(".market-tab").forEach(e=>e.classList.remove("active")),t.classList.add("active"),this.marketTab=t.dataset.tab,this.renderMarket()})}),document.getElementById("launch-btn").addEventListener("click",()=>this.launchShip()),document.getElementById("restart-btn").addEventListener("click",()=>this.restartGame()),document.getElementById("repair-btn").addEventListener("click",()=>this.payRepairs()),document.getElementById("reset-btn").addEventListener("click",()=>this.confirmReset()),document.getElementById("fuel-btn").addEventListener("click",()=>this.refuel()),document.getElementById("repair-btn-hud").addEventListener("click",()=>this.repairShip()),document.getElementById("bar-btn").addEventListener("click",()=>this.enterBar()),document.getElementById("log-btn").addEventListener("click",()=>this.showLog()),document.getElementById("log-back-btn").addEventListener("click",()=>this.hideLog()),document.getElementById("prison-continue-btn").addEventListener("click",()=>this.continueAfterPrison()),document.getElementById("bar-back-btn").addEventListener("click",()=>this.barExit()),this.bindPirateControls()}bindShellEvents(){const t=()=>{this.paused=!1,this.sound.start(),this.sound.cue("launch"),document.getElementById("start-screen").classList.add("hidden"),document.getElementById("pause-screen").classList.add("hidden"),this.lastFrame=performance.now()};document.getElementById("start-btn").addEventListener("click",t),document.getElementById("resume-btn").addEventListener("click",t),document.getElementById("pause-btn").addEventListener("click",()=>this.togglePause()),document.getElementById("mute-btn").addEventListener("click",()=>{this.sound.start();const e=this.sound.toggle();document.getElementById("mute-btn").classList.toggle("is-muted",e),document.getElementById("mute-btn").setAttribute("aria-label",e?"Включить звук":"Выключить звук")}),document.getElementById("mute-btn").classList.toggle("is-muted",this.sound.muted),document.addEventListener("keydown",e=>{e.code==="Space"&&!document.getElementById("start-screen").classList.contains("hidden")?(e.preventDefault(),t()):e.code==="Escape"&&!this.state.pirateCombat.active&&this.togglePause()}),window.__GAME_READY__=!0,window.__gameProbe=()=>({paused:this.paused,city:this.state.currentCity,credits:this.state.credits,traveling:this.state.ship.traveling,world:window.__worldStats})}togglePause(){document.getElementById("start-screen").classList.contains("hidden")&&(this.paused=!this.paused,document.getElementById("pause-screen").classList.toggle("hidden",!this.paused),this.lastFrame=performance.now())}setupMobileCityList(){let t=document.getElementById("mobile-city-list");if(!t)return;let e="";for(let i in Dt){if(i===this.state.currentCity)continue;let r=Dt[i];e+='<button class="mobile-city-btn" data-city="'+i+'" style="display:block;width:100%;padding:8px;margin-bottom:4px;background:rgba(79,195,247,0.1);border:1px solid rgba(79,195,247,0.3);border-radius:6px;color:var(--text-primary);font-family:Orbitron,monospace;font-size:10px;cursor:pointer;text-align:left;min-height:44px;"><span style="color:'+r.color+';">●</span> '+r.name+' <span style="color:var(--text-secondary);">('+r.distance+" уд)</span></button>"}t.innerHTML=e;let n=this;t.querySelectorAll(".mobile-city-btn").forEach(function(i){i.addEventListener("click",function(){n.selectCity(i.dataset.city)})}),window.innerWidth<=700&&(t.style.display="block")}selectCity(t){this.state.ship.traveling||(this.selectedCity=t,this.world?.focusCity(t),this.sound.cue("ui"),this.renderRoute(),document.getElementById("launch-btn").disabled=!1,this.notify("Выбран город: "+Dt[t].name,"success"))}handleMapClick(t){if(this.state.ship.traveling)return;let e=this.canvas.getBoundingClientRect(),n=(t.clientX-e.left)/this.canvas.width,i=(t.clientY-e.top)/this.canvas.height,r=null,a=.1;for(let o in Dt){let l=Dt[o],c=Math.sqrt(Math.pow(n-l.x,2)+Math.pow(i-l.y,2));c<a&&(a=c,r=o)}r&&r!==this.state.currentCity&&(this.selectedCity=r,this.renderRoute(),document.getElementById("launch-btn").disabled=!1)}calculateFlightStats(t,e){let n=Math.abs(Dt[t].distance-Dt[e].distance)||1,i=this.state.getTotalCargoWeight(),r=this.state.getEffectiveCargoLimit(),a=1-i/r*.4;a*=this.state.getEffectiveSpeed();let o=Math.round(lg*n/a),l=Math.round(n*n*cg*10),c=hg*(n/3)*(1+(100-this.state.shipCondition)/100);return{dist:n,duration:o,fuelCost:l,breakageChance:c}}launchShip(){if(!this.selectedCity||this.state.ship.traveling||this.state.ship.broken)return;let t=this._getRouteKey(this.state.currentCity,this.selectedCity);if(this.state.isRouteBlocked(t)){this.notify("Маршрут заблокирован событием!","error");return}if(this.state.isCityBlocked(this.selectedCity)){let a=this.state.activeEvents.find(o=>o.type==="city_block"&&o.blockedCity===this.selectedCity);if(a&&a.bypassCost)if(this.state.credits>=a.bypassCost)if(confirm("Город на карантине! Заплатить "+a.bypassCost+" CR для прохода?"))this.state.credits-=a.bypassCost,this.state.addEventToLog(a,"bypass"),this.notify("Вы заплатили пошлину: "+a.bypassCost+" CR","warning");else{this.notify("Полёт отменён","error");return}else{this.notify("Город заблокирован! Нужно "+a.bypassCost+" CR для прохода","error");return}else if(a&&a.forceLandDamage)if(confirm("Город на карантине! Приземлиться с повреждениями (-"+a.forceLandDamage+" HP)?"))this._forceLandDamage=a.forceLandDamage;else{this.notify("Полёт отменён","error");return}else{this.notify("Город заблокирован событием!","error");return}}let e=this.calculateFlightStats(this.state.currentCity,this.selectedCity),n=e.fuelCost;if(this.state.shipFuel<n){this.notify("Недостаточно топлива! Заправьте корабль","error");return}let i=.13+e.dist*.02;if(Math.random()<i){this.startPirateCombat(e.dist);return}this.state.shipFuel-=n,this.state.ship={docked:!1,traveling:!0,destination:this.selectedCity,travelProgress:0,travelDuration:e.duration,startPos:{x:Dt[this.state.currentCity].x,y:Dt[this.state.currentCity].y},endPos:{x:Dt[this.selectedCity].x,y:Dt[this.selectedCity].y},breakageChance:e.breakageChance},this.selectedCity=null;let r=document.getElementById("launch-btn");r.textContent="В ПУТИ... "+e.duration+"с",r.classList.add("traveling"),r.disabled=!0,this.sound.cue("launch"),this.notify("Взлёт! Топливо: -"+n,"warning"),this.state.save(),this.renderAll()}_getRouteKey(t,e){return[t,e].sort().join("-")}checkRandomEvent(){if(!(document.getElementById("bar-screen").classList.contains("active")||document.getElementById("prison-screen").classList.contains("active"))&&Math.random()<.15){let t=fg.filter(e=>!this.state.hasActiveEvent(e.id));if(t.length>0){let e=t[Math.floor(Math.random()*t.length)];this.activateEvent(e)}}}activateEvent(t){let e=Object.assign({},t,{remaining:t.duration});this.state.activeEvents.push(e),this.state.addEventToLog(t,"started"),this.notify("СОБЫТИЕ: "+t.name,"warning"),this.renderAll()}processEventTick(){for(let t=this.state.activeEvents.length-1;t>=0;t--){let e=this.state.activeEvents[t];e.remaining--,e.remaining<=0&&this.expireEvent(t)}}expireEvent(t){let e=this.state.activeEvents[t];this.state.activeEvents.splice(t,1),this.state.addEventToLog(e,"expired"),this.notify("Событие завершено: "+e.name,"success"),this.renderAll()}refuel(){if(this.state.ship.traveling)return;let t=Math.round(20*this.state.getFuelCostModifier());if(this.state.credits<t){this.notify("Недостаточно CR!","error");return}if(this.state.shipFuel>=100){this.notify("Бак полон!","error");return}this.state.credits-=t,this.state.shipFuel=Math.min(100,this.state.shipFuel+30),this.notify("Заправка +30 (-"+t+" CR)","success"),this.state.save(),this.renderHUD()}repairShip(){if(this.state.ship.traveling)return;let t=this.calculateRepairCost();if(this.state.credits<t){this.notify("Недостаточно CR!","error");return}if(this.state.shipCondition>=100){this.notify("Не требуется ремонт!","error");return}this.state.credits-=t,this.state.shipCondition=100,this.state.ship.broken=!1,document.getElementById("repair-screen").style.display="none",this.notify("Ремонт завершён: "+t+" CR","success"),this.state.save(),this.renderAll()}updateShip(t){if(this.state.ship.traveling&&(this.state.ship.travelProgress+=t,this.state.ship.travelProgress>=this.state.ship.travelDuration)){let e=this.state.ship.destination;if(this.state.currentCity=e,this._forceLandDamage&&(this.state.shipCondition=Math.max(0,this.state.shipCondition-this._forceLandDamage),this.notify("Принудительная посадка! Урон: -"+this._forceLandDamage+" HP","error"),this._forceLandDamage=null,this.state.shipCondition<=0&&(this.state.ship.broken=!0,document.getElementById("repair-screen").style.display="flex",document.getElementById("repair-cost").textContent="Ремонт: "+this.calculateRepairCost()+" CR")),Math.random()<this.state.ship.breakageChance){this.state.ship.broken=!0;let r=20+Math.random()*40;this.state.shipCondition=Math.max(0,this.state.shipCondition-r),document.getElementById("repair-screen").style.display="flex",document.getElementById("repair-cost").textContent="Ремонт: "+this.calculateRepairCost()+" CR"}else{let r=Math.round(this.state.ship.travelDuration/30);this.state.shipCondition=Math.min(100,this.state.shipCondition+r),this.notify("Прибыли в "+Dt[e].name+". Корабль: "+this.state.shipCondition+"%","success")}this.state.ship={docked:!0,traveling:!1,destination:null,travelProgress:0,travelDuration:0,broken:!1},this.state.tick++,this.decayFactors(),this.refreshCityStock(e),this.state.tick%2===0&&this.generateContract(),this.processEventTick(),this.checkRandomEvent(),this.state.activeEvents.find(r=>r.skip)&&(this.state.tick++,this.notify("Вы ждёте в "+Dt[e].name+" (скип тика из-за события)","warning"));let i=document.getElementById("launch-btn");i.textContent="ВЫБЕРИТЕ ГОРОД",i.classList.remove("traveling"),i.disabled=!0,this.checkWin()}}calculateRepairCost(){return Math.round(100*(1+(100-this.state.shipCondition)/50)*2*this.state.getRepairCostModifier())}payRepairs(){let t=this.calculateRepairCost();if(this.state.credits<t){this.notify("Недостаточно CR!","error");return}this.state.credits-=t,this.state.shipCondition=100,this.state.ship.broken=!1,document.getElementById("repair-screen").style.display="none",this.notify("Ремонт завершён: "+t+" CR","success"),this.state.save(),this.renderAll()}decayFactors(){for(let t in this.state.markets)for(let e in this.state.markets[t])this.state.markets[t][e].demandFactor*=.95,this.state.markets[t][e].supplyFactor*=.95}refreshCityStock(t){if(Math.random()<.4){let e=Dt[t],n=Object.keys(ue),i=n.filter(a=>e.items.includes(a)),r=n.filter(a=>!e.items.includes(a));if(i.length>3&&r.length>0){let a=Math.floor(Math.random()*i.length),o=Math.floor(Math.random()*r.length);e.items=e.items.filter(l=>l!==i[a]),e.items.push(r[o]),this.notify("В "+e.name+' товар "'+ue[r[o]].name+'" появился в продаже',"warning")}}if(Math.random()<.3){let e=Dt[t],n=Object.keys(Qe),i=e.ships.filter(a=>Qe[a]),r=n.filter(a=>!e.ships.includes(a));if(i.length>1&&r.length>0){let a=Math.floor(Math.random()*i.length),o=Math.floor(Math.random()*r.length);e.ships=e.ships.filter(l=>l!==i[a]),e.ships.push(r[o]),this.notify("В "+e.name+' появился корабль "'+Qe[r[o]].name+'"',"warning")}}}getTotalCargoWeight(){let t=0;for(let e in this.state.cargo)t+=(this.state.cargo[e]||0)*ue[e].weight;return t}generateContract(){if(this.state.contracts.length>=2)return;let t=Object.keys(Dt).filter(o=>o!==this.state.currentCity);if(t.length===0)return;let e=t[Math.floor(Math.random()*t.length)],n=Object.keys(ue),i=n[Math.floor(Math.random()*n.length)],r=Math.floor(Math.random()*5)+2,a=Math.round(ue[i].basePrice*r*og*(1+Dt[e].distance/5));this.state.contracts.push({id:Date.now(),itemId:i,quantity:r,destination:e,reward:a})}buyItem(t){if(!ue[t])return this.notify("Ошибка товара!","error"),!1;let e=this.qtyValues[t]||1,i=this.state.calculatePrice(this.state.currentCity,t)*e,r=e*ue[t].weight;if(i>this.state.credits)return this.notify("Недостаточно CR!","error"),!1;if(this.state.getTotalCargoWeight()+r>this.state.getEffectiveCargoLimit())return this.notify("Недостаточно места!","error"),!1;let a=this.state.markets[this.state.currentCity];return!a||!a[t]?(this.notify("Ошибка рынка!","error"),!1):(this.state.credits-=i,this.state.cargo[t]=(this.state.cargo[t]||0)+e,this.state.markets[this.state.currentCity][t].demandFactor+=e*.03,this.notify("Куплено "+ue[t].name+" x"+e,"success"),this.state.save(),this.renderHUD(),this.renderCargo(),this.renderRoute(),!0)}sellItem(t){let e=this.qtyValues[t]||1;if(!this.state.cargo[t]||this.state.cargo[t]<e)return this.notify("Недостаточно товара!","error"),!1;if(!ue[t])return this.notify("Ошибка товара!","error"),!1;let n=this.state.markets[this.state.currentCity];if(!n||!n[t])return this.notify("Ошибка рынка!","error"),!1;if(Cr.includes(t)&&Math.random()<.13)return this.notify("⛓ ВАС АРЕСТОВАЛИ ЗА КОНТРАБАНДУ!","error"),this.arrestPlayer(),!1;let r=this.state.calculatePrice(this.state.currentCity,t)*e*(1-hl);return this.state.credits+=r,this.state.cargo[t]-=e,this.state.cargo[t]===0&&delete this.state.cargo[t],this.state.markets[this.state.currentCity][t].supplyFactor+=e*.03,this.notify("Продано x"+e+" за "+Math.round(r)+" CR","success"),this.state.save(),this.checkWin(),this.renderHUD(),this.renderCargo(),this.renderRoute(),!0}buyShip(t){let e=Qe[t];if(!(!e||t===this.state.shipType)){if(e.price>this.state.credits){this.notify("Недостаточно CR!","error");return}if(e.slots<this.state.shipModules.length){this.notify("Не хватает слотов! Снимите модули","error");return}this.state.credits-=e.price,this.state.shipType=t,this.state.cargo={},this.notify("Куплен "+e.name+"! Груз: "+e.cargo+" кг","success"),this.state.save(),this.renderAll()}}buyModule(t){let e=nn[t];if(e){if(this.state.shipModules.includes(t)){this.notify("Модуль уже установлен!","error");return}if(!this.state.canAddModule()){this.notify("Нет свободных слотов!","error");return}if(e.price>this.state.credits){this.notify("Недостаточно CR!","error");return}this.state.credits-=e.price,this.state.shipModules.push(t),this.notify("Установлен "+e.name+" ["+e.tier+"]","success"),this.state.save(),this.renderAll()}}acceptContract(t){let e=this.state.contracts.find(i=>i.id===t);if(!e)return;let n=e.quantity*ue[e.itemId].weight;if(this.state.getTotalCargoWeight()+n>this.state.getEffectiveCargoLimit()){this.notify("Недостаточно места!","error");return}this.state.cargo[e.itemId]=(this.state.cargo[e.itemId]||0)+e.quantity,e.accepted=!0,this.selectedCity=e.destination,this.state.save(),this.renderAll(),this.renderRoute(),this.notify("Контракт: доставить "+ue[e.itemId].name+" x"+e.quantity+" в "+Dt[e.destination].name,"success")}completeContract(){let t=this.state.contracts.find(e=>e.destination===this.state.currentCity&&e.accepted);if(!t){this.notify("Нет активного контракта!","error");return}if(!this.state.cargo[t.itemId]||this.state.cargo[t.itemId]<t.quantity){this.notify("Нет нужного товара!","error");return}this.state.cargo[t.itemId]-=t.quantity,this.state.cargo[t.itemId]===0&&delete this.state.cargo[t.itemId],this.state.credits+=t.reward,this.notify("Контракт выполнен! +"+t.reward+" CR","success"),this.state.contracts=this.state.contracts.filter(e=>e.id!==t.id),this.state.save(),this.checkWin(),this.renderAll()}checkWin(){this.state.credits>=ag&&(document.getElementById("win-screen").style.display="flex")}confirmReset(){confirm("Сбросить игру? Весь прогресс будет потерян.")&&(this.state.reset(),localStorage.removeItem("cloudtrader_save"),document.getElementById("win-screen").style.display="none",document.getElementById("repair-screen").style.display="none",this.selectedCity=null,this.generateContract(),this.generateContract(),this.renderAll(),this.notify("Игра сброшена","warning"))}restartGame(){this.state.reset(),localStorage.removeItem("cloudtrader_save"),document.getElementById("win-screen").style.display="none",document.getElementById("repair-screen").style.display="none",this.selectedCity=null,this.renderAll()}notify(t,e){let n=document.getElementById("notifications"),i=document.createElement("div");i.className="notification "+e,i.textContent=t,this.sound.cue(e==="error"?"error":e==="success"?"success":"ui"),n.appendChild(i),setTimeout(()=>i.remove(),3e3)}showLog(){this.renderLog(),document.getElementById("log-screen").style.display="block"}hideLog(){document.getElementById("log-screen").style.display="none"}renderLog(){let t=document.getElementById("log-entries"),e=document.getElementById("log-panel-entries"),n="";if(this.state.eventLog.length===0)n='<div style="color:var(--text-secondary);font-size:11px;text-align:center;padding:20px;">Журнал пуст</div>';else for(let i of this.state.eventLog){let r={started:"▶ НАЧАЛОСЬ",expired:"■ ЗАВЕРШЕНО",bypass:"⚡ ПРОХОД",forceLand:"💥 ПРИНУДИТЕЛЬНАЯ ПОСАДКА"}[i.action]||i.action;n+='<div style="background:rgba(255,255,255,0.05);border-radius:6px;padding:10px;margin-bottom:8px;border-left:3px solid var(--ui-accent);"><div style="font-size:10px;color:var(--text-secondary);">Тик '+i.tick+'</div><div style="font-size:12px;color:var(--text-primary);margin:4px 0;">'+i.eventName+'</div><div style="font-size:9px;color:var(--ui-accent);">'+r+'</div><div style="font-size:10px;color:var(--text-secondary);margin-top:4px;">'+i.description+"</div></div>"}t&&(t.innerHTML=n),e&&(e.innerHTML=n)}renderAll(){this.renderHUD(),this.renderCargo(),this.renderModules(),this.renderMarket(),this.renderRoute(),this.renderLog()}renderModules(){let t=document.getElementById("modules-display"),e=this.state.getAvailableSlots(),i='<div style="font-size:9px;color:#888;margin-bottom:4px;">СЛОТЫ: '+this.state.getUsedSlots()+"/"+e+"</div>";for(let r of this.state.shipModules){let a=nn[r],o={C:"#888",B:"#4FC3F7",A:"#FFB74D",S:"#AB47BC"}[a.tier],l=a.type==="cargo"?"rgba(79,195,247,0.2)":"rgba(255,183,77,0.2)";i+='<div style="display:inline-block;background:'+l+";border-radius:3px;padding:2px 5px;margin:2px;font-size:9px;color:"+o+';">'+a.name+"</div>"}t.innerHTML=i}renderHUD(){document.getElementById("credits").innerHTML=this.state.credits.toLocaleString()+" <span>CR</span>",document.getElementById("location-info").textContent=Dt[this.state.currentCity].name,document.getElementById("tick-timer").textContent="Тик: "+this.state.tick;let t=Qe[this.state.shipType].name,e=Math.round(this.state.shipCondition),n=this.state.ship.broken?"#E57373":this.state.shipCondition<50?"#FFB74D":"#78909C";document.getElementById("ship-title").textContent="КОРАБЛЬ: "+t,document.getElementById("ship-status").textContent=this.state.ship.broken?"ПОЛОМКА!":e+"%",document.getElementById("ship-status").style.color=this.state.ship.broken?"var(--negative)":n,document.getElementById("fuel-fill").style.width=this.state.shipFuel+"%",document.getElementById("fuel-text").textContent=Math.round(this.state.shipFuel)+"%",document.getElementById("fuel-btn").disabled=this.state.ship.traveling||this.state.shipFuel>=100,document.getElementById("condition-fill").style.width=this.state.shipCondition+"%",document.getElementById("condition-fill").style.background=n,document.getElementById("condition-text").textContent=e+"%",document.getElementById("condition-text").style.color=n,document.getElementById("repair-btn-hud").style.background=n,document.getElementById("repair-btn-hud").disabled=this.state.ship.traveling||this.state.shipCondition>=100,document.getElementById("bar-btn").disabled=this.state.ship.traveling}renderCargo(){let t=this.state.getTotalCargoWeight(),e=this.state.getEffectiveCargoLimit();document.getElementById("cargo-fill").style.width=t/e*100+"%",document.getElementById("cargo-text").textContent=t+" кг / "+e+" кг";let n="";for(let i in this.state.cargo){let r=this.state.cargo[i];r>0&&(n+='<div class="cargo-item"><span class="item-name">'+dl(i)+ue[i].name+"</span><span>x"+r+"</span></div>")}n===""&&(n='<div style="color:var(--text-secondary);font-size:11px;">Пусто</div>'),document.getElementById("cargo-items").innerHTML=n}renderMarket(){document.getElementById("market-city").textContent=Dt[this.state.currentCity].name;let t=document.getElementById("market-items"),e="";if(this.marketTab==="contracts"){let n=this.state.contracts.find(i=>i.destination===this.state.currentCity&&i.accepted&&ue[i.itemId]&&Dt[i.destination]);if(n){let i=this.state.cargo[n.itemId]&&this.state.cargo[n.itemId]>=n.quantity;e+='<div class="contract-item"><img class="item-icon" src="./icons/contracts/Icon_ContractType_Standard.png" alt=""><div class="contract-header">АКТИВНЫЙ КОНТРАКТ</div><div class="contract-details">'+ue[n.itemId].name+" x"+n.quantity+" → "+Dt[n.destination].name+'</div><div class="contract-reward">+'+n.reward+' CR</div><button class="trade-btn" onclick="game.completeContract()" '+(i?"":"disabled")+">ВЫПОЛНИТЬ</button></div>"}this.state.contracts.filter(i=>!i.accepted&&ue[i.itemId]&&Dt[i.destination]).forEach(i=>{e+='<div class="contract-item"><img class="item-icon" src="./icons/contracts/Icon_ContractType_Standard.png" alt=""><div class="contract-header">'+Dt[i.destination].name+'</div><div class="contract-details">'+ue[i.itemId].name+" x"+i.quantity+'</div><div class="contract-reward">+'+i.reward+' CR</div><button class="trade-btn" onclick="game.acceptContract('+i.id+')">ПРИНЯТЬ</button></div>'}),e===""&&(e='<div style="color:var(--text-secondary);font-size:11px;">Нет контрактов</div>')}else if(this.marketTab==="ships"){let n=Dt[this.state.currentCity].ships,i=Qe[this.state.shipType];e+='<div style="color:var(--text-secondary);font-size:10px;margin-bottom:8px;">ТЕКУЩИЙ: '+i.name+" ("+i.cargo+" кг груза, скорость x"+i.speed+")</div>";for(let r of n){let a=Qe[r],o=r===this.state.shipType;e+='<div class="contract-item"><img class="ship-icon" src="'+dg(r)+'" alt=""><div class="contract-header">'+a.name+'</div><div class="contract-details">Груз: '+a.cargo+" кг | Скор: x"+a.speed+'</div><div class="contract-reward">'+(a.price===0?"Стартовый":a.price+" CR")+`</div><button class="trade-btn" onclick="game.buyShip('`+r+`')" `+(o||a.price>this.state.credits?"disabled":"")+">"+(o?"ТЕКУЩИЙ":a.price===0?"БЕСПЛАТНО":"КУПИТЬ")+"</button></div>"}n.length===0&&(e+='<div style="color:var(--text-secondary);font-size:11px;">Нет кораблей в продаже</div>')}else if(this.marketTab==="modules"){let n=this.state.getAvailableSlots(),i=this.state.getUsedSlots();e+='<div style="color:var(--text-secondary);font-size:10px;margin-bottom:8px;">СЛОТЫ: '+i+"/"+n+" | ГРУЗ: "+this.state.getEffectiveCargoLimit()+" кг | СКОР: x"+this.state.getEffectiveSpeed().toFixed(2)+"</div>";for(let r in nn){let a=nn[r],o={C:"#888",B:"#4FC3F7",A:"#FFB74D",S:"#AB47BC"}[a.tier],l=this.state.canAddModule()&&a.price<=this.state.credits;e+='<div class="contract-item"><div class="contract-header" style="color:'+o+'">'+a.name+'</div><div class="contract-details">'+(a.type==="cargo"?"+"+a.cargoBonus+" кг груза | -"+Math.round((1-a.speedMod)*100)+"% скор":"+"+Math.round(a.speedBonus*100)+"% скор | +"+a.cargoMod+" кг")+'</div><div class="contract-reward">'+a.price+` CR</div><button class="trade-btn" onclick="game.buyModule('`+r+`')" `+(l?"":"disabled")+">"+(this.state.shipModules.includes(r)?"УЖЕ ЕСТЬ":"КУПИТЬ")+"</button></div>"}}else if(this.marketTab==="ports")this.renderPortPanel(),e=document.getElementById("port-content").innerHTML;else{let n=Dt[this.state.currentCity].items,i=this.marketTab==="sell"?Object.keys(this.state.cargo).filter(a=>this.state.cargo[a]>0):n,r=this.state.hasContrabandModule();for(let a of i){let o=ue[a],l=this.state.calculatePrice(this.state.currentCity,a),c=this.marketTab==="sell"?this.state.cargo[a]||0:20;this.qtyMax[a]=c,(!this.qtyValues[a]||this.qtyValues[a]>c)&&(this.qtyValues[a]=1);let d="item-name"+(r&&Cr.includes(a)?" contraband-item":"");e+='<div class="market-item"><div class="'+d+'">'+dl(a)+"<span>"+o.name+'</span></div><div class="item-price">'+l+`</div><div class="qty-controls"><button class="qty-btn" onclick="game.changeQty('`+a+`',-1)">-</button><span class="qty-value" id="qty-`+a+'">'+this.qtyValues[a]+`</span><button class="qty-btn" onclick="game.changeQty('`+a+`',1)">+</button></div><button class="trade-btn" onclick="game.`+(this.marketTab==="buy"?"buyItem":"sellItem")+"('"+a+`')">`+(this.marketTab==="buy"?"КУПИТЬ":"ПРОДАТЬ")+"</button></div>"}e===""&&(e='<div style="color:var(--text-secondary);font-size:11px;">'+(this.marketTab==="sell"?"Нет товаров для продажи":"Нет товаров")+"</div>")}t.innerHTML=e}changeQty(t,e){let n=this.qtyMax[t]||20;this.qtyValues[t]=Math.max(1,Math.min(n,(this.qtyValues[t]||1)+e)),document.getElementById("qty-"+t).textContent=this.qtyValues[t]}renderRoute(){let t=document.getElementById("route-dest"),e=document.getElementById("route-stats"),n=document.getElementById("profit-estimate");if(this.state.ship.traveling){t.textContent="-> "+Dt[this.state.ship.destination].name,e.textContent="В пути... "+Math.ceil(this.state.ship.travelDuration-this.state.ship.travelProgress)+"с",n.textContent="";return}if(!this.selectedCity){t.textContent="Выберите город",e.textContent="",n.textContent="";return}let i=this.calculateFlightStats(this.state.currentCity,this.selectedCity);t.textContent=Dt[this.selectedCity].name+" (уд: "+Dt[this.selectedCity].distance+")";let r=this.state.shipFuel>=i.fuelCost;if(e.innerHTML="Расстояние: "+i.dist+' | Топливо: <span style="color:'+(r?"var(--positive)":"var(--negative)")+'">'+i.fuelCost+"% бака</span> | Время: "+i.duration+"с<br>Риск поломки: "+Math.round(i.breakageChance*100)+"%",Object.keys(this.state.cargo).length>0){let a=0;for(let o in this.state.cargo){let l=this.state.cargo[o];if(l>0){let c=this.state.calculatePrice(this.state.currentCity,o),h=this.state.calculatePrice(this.selectedCity,o);a+=(h-c)*l*(1-hl)}}a-=i.fuelCost*.5,n.innerHTML='<span class="'+(a>0?"profit-positive":"profit-negative")+'">Прибыль: '+Math.round(a)+" CR</span>"}else n.textContent=""}render(){this.world?.render()}drawLightning(t){let e=Math.random()*this.canvas.width,n=0;for(t.beginPath(),t.moveTo(e,n);n<this.canvas.height*.7;)e+=(Math.random()-.5)*30,n+=15+Math.random()*20,t.lineTo(e,n);t.strokeStyle="rgba(107, 91, 149, 0.6)",t.lineWidth=3,t.stroke(),t.strokeStyle="rgba(127, 219, 218, 0.7)",t.lineWidth=1.5,t.stroke()}gameLoop(){const t=performance.now(),e=Math.min(.05,(t-this.lastFrame)/1e3);this.lastFrame=t,this.paused||this.updateShip(e),this.render(),this.uiClock+=e,this.uiClock>.2&&(this.uiClock=0,this.renderHUD(),this.state.ship.traveling&&this.renderRoute()),requestAnimationFrame(()=>this.gameLoop())}stars=[];initStars(){}buyPortUpgrade(t){if(!Yn[t])return this.notify("Ошибка города!","error"),!1;if(this.state.ship.traveling)return this.notify("Нельзя улучшить порт в пути!","error"),!1;let e=this.state.getPortLevel(t),n=this.state.getPortMaxLevel(t);if(e>=n)return this.notify("Порт уже максимального уровня!","error"),!1;let i=this.state.getPortUpgradeCost(t);return this.state.credits<i?(this.notify("Недостаточно CR! Нужно: "+i,"error"),!1):(this.state.credits-=i,this.state.portLevels[t]=e+1,this.notify("Порт "+Yn[t].name+" улучшен до уровня "+(e+1)+"!","success"),this.state.save(),this.renderAll(),this.renderPortPanel(),!0)}refuel(){if(this.state.ship.traveling)return;let t=Math.round(20*this.state.getFuelCostModifier()),e=this.state.getPortFuelDiscount(this.state.currentCity);if(e>0&&(t=Math.round(t*(1-e))),this.state.credits<t){this.notify("Недостаточно CR!","error");return}if(this.state.shipFuel>=100){this.notify("Бак полон!","error");return}this.state.credits-=t,this.state.shipFuel=Math.min(100,this.state.shipFuel+30);let n=e>0?" (скидка порта "+Math.round(e*100)+"%)":"";this.notify("Заправка +30 (-"+t+" CR)"+n,"success"),this.state.save(),this.renderHUD()}updateShip(t){if(this.state.ship.traveling&&(this.state.ship.travelProgress+=t,this.state.ship.travelProgress>=this.state.ship.travelDuration)){let e=this.state.ship.destination;if(this.state.currentCity=e,this._forceLandDamage&&(this.state.shipCondition=Math.max(0,this.state.shipCondition-this._forceLandDamage),this.notify("Принудительная посадка! Урон: -"+this._forceLandDamage+" HP","error"),this._forceLandDamage=null,this.state.shipCondition<=0&&(this.state.ship.broken=!0,document.getElementById("repair-screen").style.display="flex",document.getElementById("repair-cost").textContent="Ремонт: "+this.calculateRepairCost()+" CR")),Math.random()<this.state.ship.breakageChance){this.state.ship.broken=!0;let a=20+Math.random()*40;this.state.shipCondition=Math.max(0,this.state.shipCondition-a),document.getElementById("repair-screen").style.display="flex",document.getElementById("repair-cost").textContent="Ремонт: "+this.calculateRepairCost()+" CR"}else{let a=Math.round(this.state.ship.travelDuration/30);this.state.shipCondition=Math.min(100,this.state.shipCondition+a),this.notify("Прибыли в "+Dt[e].name+". Корабль: "+this.state.shipCondition+"%","success")}this.state.ship={docked:!0,traveling:!1,destination:null,travelProgress:0,travelDuration:0,broken:!1},this.state.tick++,this.state.tick%3===0&&(this.state.timePhase=(this.state.timePhase+1)%4,this.updateTimePhaseCSS(),this.notify("Время суток: "+fl[this.state.timePhase].name,"warning")),this.decayFactors(),this.refreshCityStock(e),this.state.tick%2===0&&this.generateContract(),this.processEventTick(),this.checkRandomEvent(),this.state.activeEvents.find(a=>a.skip)&&(this.state.tick++,this.state.tick%3===0&&(this.state.timePhase=(this.state.timePhase+1)%4,this.updateTimePhaseCSS()),this.notify("Вы ждёте в "+Dt[e].name+" (скип тика из-за события)","warning"));let i=this.state.getTotalPassiveIncome();i>0&&(this.state.credits+=i,this.notify("Пассивный доход от портов: +"+i+" CR","success"));let r=document.getElementById("launch-btn");r.textContent="ВЫБЕРИТЕ ГОРОД",r.classList.remove("traveling"),r.disabled=!0,this.checkWin()}}renderPortRings(){let t=this.ctx;for(let e in Dt){let n=Dt[e],i=this.state.getPortLevel(e),r=this.state.getPortMaxLevel(e);if(i<=0)continue;let a=n.x*this.canvas.width,o=n.y*this.canvas.height,l=45,c=Math.PI*2/r;t.beginPath(),t.arc(a,o,l,0,Math.PI*2),t.strokeStyle=n.color+"30",t.lineWidth=3,t.stroke();for(let h=0;h<i;h++){let d=h*c-Math.PI/2,u=d+c;t.beginPath(),t.arc(a,o,l,d,u),t.arc(a,o,l-4,u,d,!0),t.closePath(),t.fillStyle=n.color+"AA",t.fill(),t.shadowColor=n.color,t.shadowBlur=10,t.fill(),t.shadowBlur=0}}}renderPortPanel(){let t=document.getElementById("port-content");if(!t)return;let e="";for(let i in Yn){let r=Yn[i],a=this.state.getPortLevel(i),o=r.maxLevel,l=this.state.getPortUpgradeCost(i),c=this.state.getPortFuelDiscount(i),h=this.state.getPortPassiveIncome(i),d=i===this.state.currentCity,u=a<o&&this.state.credits>=l&&!this.state.ship.traveling,p=Dt[i];e+='<div class="port-item" style="background:rgba(255,255,255,0.05);border-radius:10px;padding:12px;margin-bottom:10px;border-left:3px solid '+p.color+';"><div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:8px;"><div style="font-size:12px;color:'+p.color+';font-weight:700;">'+r.name+(d?" ★":"")+'</div><div style="font-size:10px;color:var(--text-secondary);">Уровень '+a+"/"+o+'</div></div><div style="display:flex;gap:8px;margin-bottom:8px;align-items:center;">',e+='<div style="flex:1;height:12px;background:rgba(255,248,231,0.08);border-radius:8px;overflow:hidden;"><div style="width:'+a/o*100+"%;height:100%;background:"+p.color+';border-radius:8px;transition:width 0.3s ease;"></div></div>',e+='</div><div style="font-size:9px;color:var(--text-secondary);margin-bottom:6px;">Топливо: -'+Math.round(c*100)+"% | Доход: "+h+" CR/тик</div>",a<o?e+=`<button class="trade-btn" onclick="game.buyPortUpgrade('`+i+`')" `+(u?"":"disabled")+' style="width:100%;font-size:9px;">УЛУЧШИТЬ ('+l+" CR)</button>":e+='<div style="font-size:9px;color:var(--positive);text-align:center;">МАКСИМАЛЬНЫЙ УРОВЕНЬ</div>',e+="</div>"}t.innerHTML=e;let n=document.getElementById("total-passive-income");n&&(n.textContent="Пассивный доход: "+this.state.getTotalPassiveIncome()+" CR/тик")}updateTimePhaseCSS(){let t=fl[this.state.timePhase];document.body.style.setProperty("--ui-bg",t.uiBg),document.body.style.setProperty("--text-primary",t.textPrimary),document.body.style.setProperty("--text-secondary",t.textSecondary),document.body.style.setProperty("--city-glow",t.cityGlow),document.body.style.setProperty("--sky-top",t.skyTop),document.body.style.setProperty("--sky-bottom",t.skyBottom),document.body.style.setProperty("--cloud-color",t.cloudColor),document.body.style.setProperty("--star-alpha",t.starAlpha)}arrestPlayer(){this.prisonState={daysLeft:3+Math.floor(Math.random()*8),currentDay:0,health:100,moneyEarned:0,scenarioIndex:0};let t=[];for(let e in this.state.cargo)Cr.includes(e)&&(t.push(ue[e].name+" x"+this.state.cargo[e]),delete this.state.cargo[e]);t.length>0&&this.notify("Конфисковано: "+t.join(", "),"error"),this.state.shipModules.includes("contraband_sol")&&(this.state.shipModules=this.state.shipModules.filter(e=>e!=="contraband_sol"),this.notify('Модуль "ОБХОД СОЛ" конфискован!',"error")),document.getElementById("prison-screen").classList.add("active"),this.renderPrisonUI()}renderPrisonUI(){if(!this.prisonState)return;let t=this.prisonState;document.getElementById("prison-days-left").textContent=t.daysLeft,document.getElementById("prison-health").textContent=t.health,document.getElementById("prison-money").textContent=t.moneyEarned,document.getElementById("prison-scenario-view").style.display="block",document.getElementById("prison-outcome-view").classList.remove("active");let e;t.scenarioIndex<gn.length?e=gn[t.scenarioIndex]:e=gn[Math.floor(Math.random()*gn.length)],document.getElementById("prison-day-text").textContent="ДЕНЬ "+(t.currentDay+1),document.getElementById("prison-scenario-text").textContent=e.text;let n="";for(let i=0;i<e.choices.length;i++){let r=e.choices[i],a="";r.outcome.days_lost&&(a+=" -"+r.outcome.days_lost+" дн"),r.outcome.health&&(a+=" "+(r.outcome.health>0?"+":"")+r.outcome.health+" HP"),r.outcome.money&&(a+=" "+(r.outcome.money>0?"+":"")+r.outcome.money+" CR"),r.outcome.death_chance>0&&(a+=" 💀"+r.outcome.death_chance+"%"),n+='<button class="prison-choice" onclick="game.prisonChoice('+i+')"><div>'+r.text+'</div><div class="prison-choice-outcome">'+a+"</div></button>"}document.getElementById("prison-choices").innerHTML=n}prisonChoice(t){if(!this.prisonState)return;let e=this.prisonState,i=(gn[e.scenarioIndex]||gn[Math.floor(Math.random()*gn.length)]).choices[t],r=i.outcome;if(e.currentDay++,e.daysLeft-=r.days_lost||1,e.health+=r.health||0,e.moneyEarned+=r.money||0,e.health=Math.max(0,Math.min(100,e.health)),r.death_chance&&Math.random()*100<r.death_chance){this.prisonOutcome("death",i.result);return}if(r.escape){this.prisonOutcome("escape",i.result);return}if(e.daysLeft<=0||r.release){this.prisonOutcome("release",i.result);return}e.scenarioIndex++,e.scenarioIndex>=gn.length&&(e.scenarioIndex=Math.floor(Math.random()*gn.length)),this.renderPrisonUI()}prisonOutcome(t,e){let n=document.getElementById("prison-outcome-view"),i=document.getElementById("prison-scenario-view"),r=document.getElementById("prison-outcome-title"),a=document.getElementById("prison-outcome-text"),o=document.getElementById("prison-outcome-stats"),l=document.getElementById("prison-continue-btn");if(i.style.display="none",n.classList.add("active"),t==="death")r.textContent="☠ СМЕРТЬ",r.className="prison-outcome-title death",a.textContent=e+`

Ваше тело осталось в тюрьме. Игра окончена.`,o.textContent="",l.style.display="none",setTimeout(()=>{confirm("Вы умерли в тюрьме! Начать заново?")&&(this.state.reset(),localStorage.removeItem("cloudtrader_save"),location.reload())},2e3);else if(t==="escape")r.textContent="🏃 ПОБЕГ!",r.className="prison-outcome-title release",a.textContent=e,o.textContent="Вы сбежали из тюрьмы! Статус: ОСВОБОЖДЁН",l.style.display="inline-block",l.textContent="ПРОДОЛЖИТЬ ИГРУ",this.state.cargo={};else{r.textContent="✓ ОСВОБОЖДЁН",r.className="prison-outcome-title release",a.textContent=e;let c="";this.prisonState.moneyEarned>0&&(c+="Заработано в тюрьме: +"+this.prisonState.moneyEarned+" CR. ",this.state.credits+=this.prisonState.moneyEarned),c+="Дней проведено: "+this.prisonState.currentDay,o.textContent=c,l.style.display="inline-block",l.textContent="ПРОДОЛЖИТЬ ИГРУ"}this.state.save()}continueAfterPrison(){document.getElementById("prison-screen").classList.remove("active"),this.prisonState=null,this.renderAll(),this.notify("Вы вышли на свободу!","success")}enterBar(){if(!Dt[this.state.currentCity])return;this.barState={currentScenario:null,health:this.state.shipCondition,moneyEarned:0,flags:{}};let t=Dt[this.state.currentCity].name,e={primum:'"Облачный Покоц"',secundus:'"У Барсука"',tertius:'"Туманная Капля"',quartus:'"Гхали-бар"',kibwe:'"Кривой Гикори"',elbrus:'"Вершина"'};document.getElementById("bar-location").textContent=e[this.state.currentCity]+" — "+t,document.getElementById("bar-screen").classList.add("active"),this.barShowScenario()}barShowScenario(){if(!this.barState)return;let t=this.barState;if(document.getElementById("bar-health").textContent=t.health,document.getElementById("bar-money").textContent=t.moneyEarned,!t.currentScenario){let i=Rr[Math.floor(Math.random()*Rr.length)];t.currentScenario=i}let e=t.currentScenario;document.getElementById("bar-scenario-view").style.display="block",document.getElementById("bar-outcome-view").classList.remove("active"),document.getElementById("bar-title-text").textContent=e.title,document.getElementById("bar-scenario-text").textContent=e.text;let n="";for(let i=0;i<e.choices.length;i++){let r=e.choices[i],a="";r.outcome.health&&(a+=(r.outcome.health>0?"+":"")+r.outcome.health+" HP"),r.outcome.money&&(a+=(r.outcome.money>0?"+":"")+r.outcome.money+" CR"),n+='<button class="bar-choice" onclick="game.barChoice('+i+')"><div>'+r.text+"</div>"+(a?'<div class="bar-choice-outcome">'+a+"</div>":"")+"</button>"}document.getElementById("bar-choices").innerHTML=n}barChoice(t){if(!this.barState)return;let e=this.barState,i=e.currentScenario.choices[t],r=i.outcome;if(e.health+=r.health||0,e.health=Math.max(0,Math.min(100,e.health)),i.exit){this.barOutcome(i.result,r);return}if(i.next){let a=Rr.find(o=>o.id===i.next);a?(e.currentScenario=a,this.barShowScenario()):this.barExit();return}this.barExit()}barOutcome(t,e){let n=document.getElementById("bar-outcome-view"),i=document.getElementById("bar-scenario-view");i.style.display="none",n.classList.add("active");let r=document.getElementById("bar-outcome-title"),a=document.getElementById("bar-outcome-text"),o=document.getElementById("bar-outcome-stats");r.textContent=t.title,r.className="bar-outcome-title "+t.type,a.textContent=t.text;let l="";if(e.health&&(l+=(e.health>0?"+":"")+e.health+" HP. "),e.money){let h=e.money;h>0?(this.state.credits+=h,this.barState.moneyEarned+=h):this.state.credits=Math.max(0,this.state.credits+h),l+=(h>0?"+":"")+h+" CR. "}this.state.shipCondition!==this.barState.health&&(this.state.shipCondition=this.barState.health),o.textContent=l||"Без изменений";let c=document.getElementById("bar-continue-btn");c.onclick=()=>this.barExit()}barExit(){this.barState&&(this.state.shipCondition!==this.barState.health&&(this.state.shipCondition=this.barState.health),this.barState.moneyEarned>0&&this.notify("Заработано в баре: +"+this.barState.moneyEarned+" CR","success"),this.state.save()),document.getElementById("bar-screen").classList.remove("active"),this.barState=null,this.renderAll()}startPirateCombat(t){let e=t>=4&&Math.random()<.05,n=e?1:Math.floor(2+Math.random()*3),i=this.state.pirateCombat;i.active=!0,i.playerHP=3,i.enemies=[],i.bullets=[],i.enemyBullets=[],i.score=0,i.playerX=400,i.playerY=500,i.playerAngle=-Math.PI/2,i.lastShot=0,i.isBoss=e,i.nearestCity=this.state.currentCity,i.gameLoopId=null,i.lastTime=0,i.keys={},i.mouse={x:400,y:500,pressed:!1},i.touch={aiming:!1,touchAimX:0,touchAimY:0},i.phase="countdown",i.countdown=5,this.initPirateEnemies(n,e);let r=document.getElementById("pirate-screen");r.style.display="flex",r.classList.add("active");let a=document.getElementById("pirate-canvas"),o=window.innerWidth<800||"ontouchstart"in window&&window.innerWidth<1024,l=document.getElementById("pirate-mobile-controls");if(o?l.classList.add("active"):l.classList.remove("active"),o){let h=window.innerWidth*.98,d=window.innerHeight*.55,u=800/600,p=h,g=p/u;g>d&&(g=d,p=g*u),a.width=Math.floor(p),a.height=Math.floor(g),i.scale=.6}else a.width=800,a.height=600,i.scale=1;i.touch.touchStartX=100*i.scale,i.touch.touchStartY=a.height-100*i.scale,a.getContext("2d").clearRect(0,0,a.width,a.height),this.updatePirateHUD(),this.bindPirateControls(),this.notify("☠ ПИРАТЫ АТАКУЮТ!","error"),this.pirateCountdownInterval=setInterval(()=>{i.countdown--,this.renderPirateCombat(),i.countdown<=0&&(clearInterval(this.pirateCountdownInterval),this.pirateCountdownInterval=null,i.phase="combat",i.lastTime=0,i.gameLoopId=requestAnimationFrame(h=>this.pirateGameLoop(h)))},1e3)}pirateGameLoop(t){let e=this.state.pirateCombat;if(!e.active)return;e.lastTime===0&&(e.lastTime=t);let n=Math.min((t-e.lastTime)/1e3,.1);if(e.lastTime=t,e.phase==="countdown"){this.renderPirateCombat(),e.gameLoopId=requestAnimationFrame(i=>this.pirateGameLoop(i));return}this.updatePirateCombat(n),this.renderPirateCombat(),e.gameLoopId=requestAnimationFrame(i=>this.pirateGameLoop(i))}initPirateEnemies(t,e){let n=this.state.pirateCombat,i=document.getElementById("pirate-canvas"),r=n.scale||1;if(e)n.enemies.push({x:i.width/2,y:-50*r,vx:1.5,vy:.5,hp:10,maxHP:10,size:35,isBoss:!0,shootTimer:0,speed:1.2});else for(let a=0;a<t;a++){let o=80*r;n.enemies.push({x:o+Math.random()*(i.width-o*2),y:-50-Math.random()*50,vx:1+Math.random()*2,vy:.5+Math.random()*1,hp:2,maxHP:2,size:20,isBoss:!1,shootTimer:Math.random()*2,speed:.8+Math.random()*1.2})}}updatePirateCombat(t){let e=this.state.pirateCombat,n=document.getElementById("pirate-canvas");if(e.phase!=="combat")return;let i=200;if((e.keys.w||e.keys.W||e.keys.ArrowUp)&&(e.playerY-=i*t),(e.keys.s||e.keys.S||e.keys.ArrowDown)&&(e.playerY+=i*t),(e.keys.a||e.keys.A||e.keys.ArrowLeft)&&(e.playerX-=i*t),(e.keys.d||e.keys.D||e.keys.ArrowRight)&&(e.playerX+=i*t),e.playerX=Math.max(20,Math.min(n.width-20,e.playerX)),e.playerY=Math.max(20,Math.min(n.height-20,e.playerY)),e.touch.aiming){let a=n.getBoundingClientRect(),o=e.touchAimX-a.left,l=e.touchAimY-a.top;e.playerAngle=Math.atan2(l-e.playerY,o-e.playerX)}else{let a=e.mouse.x||e.playerX,o=e.mouse.y||e.playerY;e.playerAngle=Math.atan2(o-e.playerY,a-e.playerX)}e.lastShot+=t,(e.keys[" "]||e.mouse.pressed)&&e.lastShot>.2&&(this.shootBullet(e.playerX,e.playerY,e.playerAngle,!0),e.lastShot=0);for(let a=e.bullets.length-1;a>=0;a--){let o=e.bullets[a];o.x+=Math.cos(o.angle)*o.speed*t,o.y+=Math.sin(o.angle)*o.speed*t,(o.x<0||o.x>n.width||o.y<0||o.y>n.height)&&e.bullets.splice(a,1)}for(let a=e.enemyBullets.length-1;a>=0;a--){let o=e.enemyBullets[a];o.x+=Math.cos(o.angle)*o.speed*t,o.y+=Math.sin(o.angle)*o.speed*t,(o.x<0||o.x>n.width||o.y<0||o.y>n.height)&&e.enemyBullets.splice(a,1)}for(let a=e.enemies.length-1;a>=0;a--){let o=e.enemies[a],l=e.scale||1,c=30*l,h=e.playerX-o.x,d=e.playerY-o.y,u=Math.sqrt(h*h+d*d);if(u>0&&(o.x+=h/u*o.speed*l,o.y+=d/u*o.speed*l),o.x<c&&(o.x=c),o.x>n.width-c&&(o.x=n.width-c),o.y<c&&(o.y=c),o.y>n.height-c*3&&(o.y=n.height-c*3),o.shootTimer+=t,u<300&&o.shootTimer>(o.isBoss?.8:1.8)){let p=Math.atan2(d,h);this.shootBullet(o.x,o.y,p,!1),o.shootTimer=0}for(let p=e.bullets.length-1;p>=0;p--){let g=e.bullets[p],v=g.x-o.x,m=g.y-o.y;if(Math.sqrt(v*v+m*m)<o.size){o.hp--,e.bullets.splice(p,1),o.hp<=0&&(e.score+=o.isBoss?100:20,e.enemies.splice(a,1));break}}}for(let a=e.enemyBullets.length-1;a>=0;a--){let o=e.enemyBullets[a],l=o.x-e.playerX,c=o.y-e.playerY;if(Math.sqrt(l*l+c*c)<15&&(e.playerHP--,e.enemyBullets.splice(a,1),this.updatePirateHUD(),e.playerHP<=0)){this.endPirateCombat(!1);return}}if(e.enemies.length===0){this.endPirateCombat(!0);return}this.updatePirateHUD()}renderPirateCombat(){let t=this.state.pirateCombat,e=document.getElementById("pirate-canvas"),n=e.getContext("2d"),i=t.scale||1;n.fillStyle="#0a0000",n.fillRect(0,0,e.width,e.height),n.strokeStyle="rgba(229, 115, 115, 0.08)",n.lineWidth=1;for(let r=0;r<e.width;r+=40*i)n.beginPath(),n.moveTo(r,0),n.lineTo(r,e.height),n.stroke();for(let r=0;r<e.height;r+=40*i)n.beginPath(),n.moveTo(0,r),n.lineTo(e.width,r),n.stroke();n.save(),n.translate(t.playerX,t.playerY),n.rotate(t.playerAngle),n.scale(i,i),n.fillStyle="#7FDBDA",n.beginPath(),n.moveTo(18,0),n.lineTo(-12,-10),n.lineTo(-6,0),n.lineTo(-12,10),n.closePath(),n.fill(),n.strokeStyle="#FFF8E7",n.lineWidth=2,n.stroke(),n.restore(),n.fillStyle="#80DEEA",n.shadowColor="#80DEEA",n.shadowBlur=8*i;for(let r of t.bullets)n.beginPath(),n.arc(r.x,r.y,5*i,0,Math.PI*2),n.fill();n.shadowBlur=0,n.fillStyle="#FFB74D",n.shadowColor="#FFB74D",n.shadowBlur=6*i;for(let r of t.enemyBullets)n.beginPath(),n.arc(r.x,r.y,6*i,0,Math.PI*2),n.fill();n.shadowBlur=0;for(let r of t.enemies){n.fillStyle=r.isBoss?"#E57373":"#FF6F00",n.beginPath(),n.arc(r.x,r.y,r.size*i,0,Math.PI*2),n.fill(),n.strokeStyle="#FFF",n.lineWidth=2*i,n.stroke(),r.isBoss&&(n.fillStyle="rgba(229, 115, 115, 0.3)",n.beginPath(),n.arc(r.x,r.y,(r.size+8)*i,0,Math.PI*2),n.fill());let a=r.size*2*i,o=5*i;n.fillStyle="rgba(0,0,0,0.6)",n.fillRect(r.x-a/2,r.y-r.size*i-12*i,a,o),n.fillStyle=r.hp>r.maxHP*.5?"#81C784":"#E57373",n.fillRect(r.x-a/2,r.y-r.size*i-12*i,a*(r.hp/r.maxHP),o)}n.fillStyle="#FFF8E7",n.font="bold "+18*i+"px Orbitron, monospace",n.textAlign="right",n.fillText("СЧЁТ: "+t.score,e.width-20*i,30*i),t.phase==="countdown"&&(n.fillStyle="#E57373",n.font="bold "+72*i+"px Orbitron, monospace",n.textAlign="center",n.fillText(t.countdown.toString(),e.width/2,e.height/2),n.font="bold "+24*i+"px Orbitron, monospace",n.fillText("ПРИГОТОВЬТЕСЬ!",e.width/2,e.height/2-80*i))}endPirateCombat(t){let e=this.state.pirateCombat;e.active=!1,this.pirateCountdownInterval&&(clearInterval(this.pirateCountdownInterval),this.pirateCountdownInterval=null),e.gameLoopId&&(cancelAnimationFrame(e.gameLoopId),e.gameLoopId=null),e._cleanup&&e._cleanup(),e.lastTime=0,e.keys={},e.mouse={x:0,y:0,pressed:!1};let n=document.getElementById("pirate-screen");if(n.classList.remove("active"),n.style.display="none",document.getElementById("pirate-mobile-controls").classList.remove("active"),t){let r=50+e.score;this.state.credits+=r,this.notify("☠ ПИРАТЫ ПОБЕЖДЕНЫ! +"+r+" CR","success");let a=this.selectedCity||this.state.ship.destination,o=this.calculateFlightStats(this.state.currentCity,a);this.state.shipFuel-=o.fuelCost,this.state.ship={docked:!1,traveling:!0,destination:a,travelProgress:0,travelDuration:o.duration,startPos:{x:Dt[this.state.currentCity].x,y:Dt[this.state.currentCity].y},endPos:{x:Dt[a].x,y:Dt[a].y},breakageChance:o.breakageChance},this.selectedCity=null;let l=document.getElementById("launch-btn");l.textContent="В ПУТИ... "+o.duration+"с",l.classList.add("traveling"),l.disabled=!0,this.state.save(),this.renderAll()}else{Object.keys(this.state.cargo).length>0?(this.state.cargo={},this.state.shipCondition=Math.max(0,this.state.shipCondition-30),this.notify("☠ КОРАБЛЬ ЗАХВАЧЕН! Груз потерян, -30 HP","error")):(this.state.shipType="dinghy",this.state.shipModules=[],this.state.credits=Math.max(500,this.state.credits),this.state.shipCondition=100,this.state.shipFuel=100,this.notify("☠ КОРАБЛЬ ПОТЕРЯН! Возрождение в "+Dt[e.nearestCity].name+" с 500 CR","error")),this.state.ship={docked:!0,traveling:!1,destination:null,travelProgress:0,travelDuration:0,broken:!1},this.state.currentCity=e.nearestCity;let a=document.getElementById("launch-btn");a.textContent="ВЫБЕРИТЕ ГОРОД",a.classList.remove("traveling"),a.disabled=!0,this.state.save(),this.renderAll()}}shootBullet(t,e,n,i){let r={x:t,y:e,angle:n,speed:i?450:220};i?this.state.pirateCombat.bullets.push(r):this.state.pirateCombat.enemyBullets.push(r)}updatePirateHUD(){let t=this.state.pirateCombat,e=document.getElementById("pirate-health"),n=document.getElementById("pirate-enemies");e&&(e.textContent="HP: "+t.playerHP+"/3"),n&&(n.textContent="Врагов: "+t.enemies.length)}bindPirateControls(){let t=document.getElementById("pirate-canvas"),e=this.state.pirateCombat,n=u=>{if(!e.active)return;let p=u.key;u.key.length===1&&(p=u.key.toLowerCase()),e.keys[p]=u.type==="keydown"},i=u=>{if(!e.active)return;let p=t.getBoundingClientRect();e.mouse.x=u.clientX-p.left,e.mouse.y=u.clientY-p.top},r=u=>{e.active&&(e.mouse.pressed=!0)},a=u=>{e.active&&(e.mouse.pressed=!1)};document.addEventListener("keydown",n),document.addEventListener("keyup",n),t.addEventListener("mousemove",i),t.addEventListener("mousedown",r),t.addEventListener("mouseup",a),document.querySelectorAll(".pirate-dpad-btn").forEach(u=>{let p=u.dataset.dir,g=p==="up"?"ArrowUp":p==="down"?"ArrowDown":p==="left"?"ArrowLeft":"ArrowRight",v=f=>{f.preventDefault(),f.stopPropagation(),e.keys[g]=!0,u.classList.add("pressed")},m=f=>{f.preventDefault(),f.stopPropagation(),e.keys[g]=!1,u.classList.remove("pressed")};u.addEventListener("touchstart",v,{passive:!1}),u.addEventListener("touchend",m,{passive:!1}),u.addEventListener("touchcancel",m,{passive:!1}),u.addEventListener("mousedown",v),u.addEventListener("mouseup",m),u.addEventListener("mouseleave",m)});let o=document.getElementById("pirate-aim"),l=null,c=u=>{u.preventDefault(),u.stopPropagation();let p=u.changedTouches[0];l=p.identifier,e.touch.aiming=!0,e.touchAimX=p.clientX,e.touchAimY=p.clientY,e.mouse.pressed=!0,o.classList.add("aiming")},h=u=>{if(l!==null){u.preventDefault();for(let p=0;p<u.changedTouches.length;p++){let g=u.changedTouches[p];if(g.identifier===l){e.touchAimX=g.clientX,e.touchAimY=g.clientY;break}}}},d=u=>{if(l!==null){u.preventDefault();for(let p=0;p<u.changedTouches.length;p++)if(u.changedTouches[p].identifier===l){l=null,e.touch.aiming=!1,e.mouse.pressed=!1,o.classList.remove("aiming");break}}};o.addEventListener("touchstart",c,{passive:!1}),o.addEventListener("touchmove",h,{passive:!1}),o.addEventListener("touchend",d,{passive:!1}),o.addEventListener("touchcancel",d,{passive:!1}),e._cleanup=()=>{document.removeEventListener("keydown",n),document.removeEventListener("keyup",n),t.removeEventListener("mousemove",i),t.removeEventListener("mousedown",r),t.removeEventListener("mouseup",a),document.querySelectorAll(".pirate-dpad-btn").forEach(u=>{let p=u.dataset.dir,g=p==="up"?"ArrowUp":p==="down"?"ArrowDown":p==="left"?"ArrowLeft":"ArrowRight";e.keys[g]=!1}),o.removeEventListener("touchstart",c),o.removeEventListener("touchmove",h),o.removeEventListener("touchend",d),o.removeEventListener("touchcancel",d)}}unbindPirateControls(){let t=this.state.pirateCombat;t._cleanup&&t._cleanup()}}function Ql(s){document.querySelectorAll(".mobile-tab").forEach(n=>{n.classList.remove("active"),n.dataset.panel===s&&n.classList.add("active")}),document.querySelectorAll("#bottom-panels .panel").forEach(n=>{n.classList.remove("mobile-active"),n.style.removeProperty("display")});let t=document.getElementById(s);t&&(t.classList.add("mobile-active"),t.style.removeProperty("display"));let e=document.getElementById("bottom-panels");window.innerWidth<=700&&e.classList.add("mobile-visible")}window.innerWidth<=700&&(document.getElementById("mobile-tabs").style.display="flex",document.getElementById("bottom-panels").classList.add("mobile-visible"),document.getElementById("ship-panel").classList.add("mobile-active"));window.addEventListener("resize",function(){let s=document.getElementById("mobile-tabs");window.innerWidth<=700?(s.style.display="flex",document.getElementById("bottom-panels").classList.add("mobile-visible")):(s.style.display="none",document.getElementById("bottom-panels").classList.remove("mobile-visible"))});let gg=new mg;window.game=gg;window.switchMobileTab=Ql;Ql("ship-panel");
