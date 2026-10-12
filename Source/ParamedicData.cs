using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Paramedic;

/// <summary>Сохраняемые данные одной пешки-парамедика.</summary>
public class ParamedicRecord : IExposable
{
    public int dutyTicks;                       // суммарное время на смене
    public int saved;                           // спасённые жизни (дублирует RecordDef для расчёта звания)
    public List<int> violationTicks = new();    // нарушения клятвы (для «Кризиса веры»)
    public List<int> patientDeathTicks = new(); // смерти пациентов (для «Выгорания»)
    public int shoutBlockedUntil;               // блок крика после вероломства
    public Dictionary<string, int> cooldownEnd = new(); // перезарядки снятых способностей
    public int prevMedicineStock = -1;          // запас медикаментов до надевания жилета

    public bool OathTaken => dutyTicks >= GenDate.TicksPerDay;

    public void ExposeData()
    {
        Scribe_Values.Look(ref dutyTicks, "dutyTicks");
        Scribe_Values.Look(ref saved, "saved");
        Scribe_Collections.Look(ref violationTicks, "violationTicks", LookMode.Value);
        Scribe_Collections.Look(ref patientDeathTicks, "patientDeathTicks", LookMode.Value);
        Scribe_Values.Look(ref shoutBlockedUntil, "shoutBlockedUntil");
        Scribe_Collections.Look(ref cooldownEnd, "cooldownEnd", LookMode.Value, LookMode.Value);
        Scribe_Values.Look(ref prevMedicineStock, "prevMedicineStock", -1);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            violationTicks ??= new();
            patientDeathTicks ??= new();
            cooldownEnd ??= new();
        }
    }
}

/// <summary>Пациент, которого лечил парамедик: через сутки живой — «спасён».</summary>
public class PendingPatient : IExposable
{
    public Pawn patient;
    public Pawn doctor;
    public int tick;

    public void ExposeData()
    {
        Scribe_References.Look(ref patient, "patient");
        Scribe_References.Look(ref doctor, "doctor");
        Scribe_Values.Look(ref tick, "tick");
    }
}

public class GameComponent_Paramedic : GameComponent
{
    public static GameComponent_Paramedic Instance;

    private Dictionary<Pawn, ParamedicRecord> records = new();
    private List<PendingPatient> pending = new();
    private HashSet<Pawn> humanitarianPatients = new();
    private Dictionary<Faction, int> lastHumanitarianTick = new();

    // Кто последним ранил пешку (не сохраняется): смерть от кровопотери засчитывается стрелку.
    public readonly Dictionary<Pawn, (Pawn attacker, int tick)> lastHarm = new();

    private List<Pawn> tmpPawns; private List<ParamedicRecord> tmpRecords;
    private List<Faction> tmpFactions; private List<int> tmpTicks;

    public GameComponent_Paramedic(Game game) { Instance = this; }

    public ParamedicRecord Get(Pawn pawn, bool create = true)
    {
        if (pawn == null) return null;
        if (!records.TryGetValue(pawn, out var r) && create)
        {
            r = new ParamedicRecord();
            records[pawn] = r;
        }
        return r;
    }

    public void AddPending(Pawn patient, Pawn doctor)
    {
        for (int i = 0; i < pending.Count; i++)
        {
            if (pending[i].patient == patient && pending[i].doctor == doctor)
            {
                return;
            }
        }
        pending.Add(new PendingPatient { patient = patient, doctor = doctor, tick = Find.TickManager.TicksGame });
    }

    public void MarkHumanitarian(Pawn patient) => humanitarianPatients.Add(patient);

    public bool TakeHumanitarian(Pawn patient) => humanitarianPatients.Remove(patient);

    public bool HumanitarianAllowed(Faction f)
    {
        int now = Find.TickManager.TicksGame;
        if (lastHumanitarianTick.TryGetValue(f, out int t) && now - t < GenDate.TicksPerDay)
        {
            return false;
        }
        lastHumanitarianTick[f] = now;
        return true;
    }

    /// <summary>Пациент умер: у всех парамедиков, лечивших его последние сутки, — «Не спас».</summary>
    public void Notify_PatientDied(Pawn patient)
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            var p = pending[i];
            if (p.patient != patient) continue;
            pending.RemoveAt(i);
            if (p.doctor != null && !p.doctor.Dead)
            {
                OathUtility.Notify_PatientLost(p.doctor);
            }
        }
        humanitarianPatients.Remove(patient);
    }

    public override void GameComponentTick()
    {
        int now = Find.TickManager.TicksGame;
        if (now % 250 != 0) return;

        // Время на смене
        foreach (var kv in records)
        {
            if (kv.Key.Spawned && ParamedicUtility.IsOnDuty(kv.Key))
            {
                kv.Value.dutyTicks += 250;
            }
        }

        // Спасённые: сутки после лечения, пациент жив
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            var p = pending[i];
            if (p.patient == null || p.doctor == null || p.patient.Destroyed && !p.patient.Dead)
            {
                pending.RemoveAt(i);
                continue;
            }
            if (now - p.tick < GenDate.TicksPerDay) continue;
            pending.RemoveAt(i);
            if (!p.patient.Dead && !p.doctor.Dead)
            {
                RankUtility.Notify_LifeSaved(p.doctor);
            }
        }

        if (now % 2500 == 0)
        {
            lastHarm.RemoveAll(kv => kv.Key.Dead || now - kv.Value.tick > GenDate.TicksPerHour * 2);
        }
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref records, "records", LookMode.Reference, LookMode.Deep, ref tmpPawns, ref tmpRecords);
        Scribe_Collections.Look(ref pending, "pending", LookMode.Deep);
        Scribe_Collections.Look(ref humanitarianPatients, "humanitarianPatients", LookMode.Reference);
        Scribe_Collections.Look(ref lastHumanitarianTick, "lastHumanitarianTick", LookMode.Reference, LookMode.Value, ref tmpFactions, ref tmpTicks);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            records ??= new();
            records.RemoveAll(kv => kv.Key == null);
            pending ??= new();
            pending.RemoveAll(p => p.patient == null || p.doctor == null);
            humanitarianPatients ??= new();
            humanitarianPatients.RemoveWhere(p => p == null);
            lastHumanitarianTick ??= new();
            lastHumanitarianTick.RemoveAll(kv => kv.Key == null);
        }
    }
}
