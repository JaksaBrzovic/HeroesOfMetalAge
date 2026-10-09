using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfMetalAge
{
    // Original synthesized prototype audio. Replace clips with authored stems without changing gameplay events.
    public class MetalAudio : MonoBehaviour
    {
        AudioSource ambient, gallop, combat, sting, voice, effects;
        Dictionary<string,AudioClip> clips = new Dictionary<string,AudioClip>();
        public float musicVolume=.52f, sfxVolume=.7f, voiceVolume=.65f;
        public bool riding, fighting, muted;
        float lastHit=-10;
        const int Rate=22050;
        public void Initialize()
        {
            ambient=Source(true); gallop=Source(true); combat=Source(true); sting=Source(false); voice=Source(false); effects=Source(false);
            ambient.clip=Make("ambient",9.6f,0); gallop.clip=Make("gallop",9.6f,1); combat.clip=Make("battle",9.6f,2);
            foreach(string name in new[]{"friendly","enemy","intro","victory","defeat","gold","equip","level","hit","heavy","hurt","click"})
                clips[name]=Make(name, name=="victory" || name=="defeat" ? 2.4f : name=="intro" ? 1.2f : .6f,3);
            double start=AudioSettings.dspTime+.15;
            ambient.PlayScheduled(start); gallop.PlayScheduled(start); combat.PlayScheduled(start);
        }
        AudioSource Source(bool loop) { var s=gameObject.AddComponent<AudioSource>(); s.loop=loop; s.playOnAwake=false; s.spatialBlend=0; s.volume=0; return s; }
        void Update()
        {
            if(ambient==null) return;
            float m=muted?0:musicVolume;
            ambient.volume=Mathf.Lerp(ambient.volume,m*(fighting?.17f:riding?.4f:.7f),Time.unscaledDeltaTime*3);
            gallop.volume=Mathf.Lerp(gallop.volume,riding&&!fighting?m*.72f:0,Time.unscaledDeltaTime*5);
            combat.volume=Mathf.Lerp(combat.volume,fighting?m*.85f:0,Time.unscaledDeltaTime*3);
            sting.volume=effects.volume=muted?0:sfxVolume; voice.volume=muted?0:voiceVolume;
        }
        public void Cue(string id)
        {
            if(!clips.ContainsKey(id)) return;
            if(id=="click" || id=="gold" || id=="equip") effects.PlayOneShot(clips[id],.45f);
            else { sting.Stop(); sting.clip=clips[id]; sting.Play(); }
        }
        public void Hit(int damage, bool received)
        {
            if(Time.unscaledTime-lastHit<.16f) return;
            lastHit=Time.unscaledTime; effects.PlayOneShot(clips[received?"hurt":damage>=45?"heavy":"hit"],.75f);
        }
        public void Voice(int kind)
        {
            var clip=Resources.Load<AudioClip>("Voices/"+Rules.Units[kind].voice);
            if(clip==null) return; voice.Stop(); voice.pitch=kind==0?.83f:kind==1?.96f:.73f; voice.clip=clip; voice.Play();
        }
        public void StopBattle() { fighting=false; riding=false; }
        AudioClip Make(string name,float seconds,int mode)
        {
            int length=Mathf.RoundToInt(seconds*Rate); var data=new float[length];
            uint seed=12345;foreach(char letter in name)seed=seed*31+letter; int[] notes={0,0,3,0,5,3,0,-2,0,0,7,5,3,0,-2,-2};
            for(int i=0;i<length;i++)
            {
                float t=(float)i/Rate, beat=t/.6f, phase=beat-Mathf.Floor(beat), sample=0;
                seed=1664525*seed+1013904223; float noise=((seed>>8)/(float)0xFFFFFF)*2-1;
                if(mode==0)
                {
                    sample=.10f*Mathf.Sin(2*Mathf.PI*82.4069f*t)+.07f*Mathf.Sin(2*Mathf.PI*123.47f*t)+.035f*Mathf.Sin(2*Mathf.PI*164.81f*t);
                    sample*=.7f+.3f*Mathf.Sin(t*.65f); sample+=noise*.005f;
                }
                else if(mode==1 || mode==2)
                {
                    float step=t/.15f, p=step-Mathf.Floor(step);
                    int note=notes[Mathf.FloorToInt(step)%notes.Length]; float f=82.4069f*Mathf.Pow(2,note/12f);
                    float guitar=Mathf.Sin(2*Mathf.PI*f*t)+.45f*Mathf.Sin(2*Mathf.PI*f*1.5f*t)+.25f*Mathf.Sin(2*Mathf.PI*f*2*t);
                    guitar=(float)System.Math.Tanh(guitar*3f);
                    sample=guitar*Mathf.Exp(-p*(mode==1?6:3.6f))*(mode==1?.16f:.22f);
                    float kick=Mathf.Sin(2*Mathf.PI*(48*t+2*(1-Mathf.Exp(-phase*18))))*Mathf.Exp(-phase*22);
                    sample+=kick*.24f;
                    if((Mathf.FloorToInt(beat)%2)==1) sample+=noise*Mathf.Exp(-phase*20)*.12f;
                    sample+=noise*Mathf.Exp(-p*25)*.035f;
                }
                else
                {
                    bool positive=name=="friendly" || name=="victory" || name=="level" || name=="gold" || name=="equip";
                    bool impact=name=="hit" || name=="heavy" || name=="hurt";
                    int step=Mathf.FloorToInt(t/.15f); int[] riff=positive?new[]{0,4,7,12,7,4,0,7}:new[]{0,1,-2,0,-5,-2,1,0};
                    float root=name=="gold"?440:name=="equip"?330:name=="level"?220:name=="friendly"||name=="victory"?164.81f:name=="enemy"?82.4069f:110;
                    float f=root*Mathf.Pow(2,riff[step%riff.Length]/12f);
                    float env=Mathf.Exp(-(t% .15f)*14)*Mathf.Clamp01((seconds-t)/.15f);
                    sample=(float)System.Math.Tanh((Mathf.Sin(2*Mathf.PI*f*t)+.5f*Mathf.Sin(2*Mathf.PI*f*1.5f*t))*3)*env*.24f;
                    if(impact) {
                        float decay=name=="heavy"?7:name=="hurt"?10:20;
                        float tone=name=="heavy"?48:name=="hurt"?37:105;
                        sample=(noise*(name=="heavy"?.27f:.14f)+Mathf.Sin(2*Mathf.PI*tone*t)*.32f)*Mathf.Exp(-t*decay);
                        if(name=="hurt")sample+=Mathf.Sin(2*Mathf.PI*41.5f*t)*.22f*Mathf.Exp(-t*9);
                    }
                    if(name=="click") sample=Mathf.Sin(2*Mathf.PI*880*t)*Mathf.Exp(-t*80)*.1f;
                    if(name=="defeat") sample*=Mathf.Exp(-t*.8f);
                }
                data[i]=Mathf.Clamp(sample,-.8f,.8f);
            }
            var clip=AudioClip.Create(name,length,1,Rate,false); clip.SetData(data,0); return clip;
        }
        void OnDestroy()
        {
            foreach(var c in clips.Values) Destroy(c);
            if(ambient!=null) Destroy(ambient.clip); if(gallop!=null) Destroy(gallop.clip); if(combat!=null) Destroy(combat.clip);
        }
    }
}
