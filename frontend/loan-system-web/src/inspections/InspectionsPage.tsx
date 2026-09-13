import { FormEvent, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { loanApplicationsApi } from '../api/loanApplications';
import { Inspection, InspectionInput, inspectionsApi } from '../api/inspections';
import { ApiError } from '../api/identity';
import { hasPermission } from '../app/permissions';

function errorKey(error: unknown) {
  if (!(error instanceof ApiError)) return 'inspectionError';
  if (error.status === 412) return 'inspectionConcurrency';
  return ({
    'inspections.finalized': 'inspectionFinalized',
    'inspections.invalidState': 'inspectionInvalidState',
    'inspections.reinspectionNotSupported': 'reinspectionNotSupported',
  } as Record<string, string>)[error.errorCode ?? ''] ?? (error.status === 422 ? 'inspectionValidation' : 'inspectionError');
}

export function InspectionsPage({ permissions }: { permissions: string[] }) {
  const { t } = useTranslation();
  const queue = useQuery({
    queryKey: ['inspection-work-queue'],
    queryFn: async () => {
      const [waiting, pending, ready] = await Promise.all([
        loanApplicationsApi.list('status=CommitteeApproved&pageSize=100'),
        loanApplicationsApi.list('status=PrerequisitesPending&pageSize=100'),
        loanApplicationsApi.list('status=ReadyForFinalApproval&pageSize=100'),
      ]);
      const applications = [...pending.items, ...ready.items];
      const existing = (await Promise.all(applications.map(async application => ({
        application,
        inspections: await inspectionsApi.list(application.loanApplicationId),
      })))).flatMap(x => x.inspections.map(inspection => ({ inspection, borrowerName: x.application.borrowerName })));
      return { waiting: waiting.items, existing };
    },
  });
  const [selected, setSelected] = useState<Inspection | null>(null);
  const [error, setError] = useState<unknown>();
  if (selected) return <InspectionForm initial={selected} permissions={permissions} close={() => setSelected(null)} />;
  return <section>
    <h1>{t('inspections')}</h1>
    {error !== undefined && <p role="alert">{t(errorKey(error))}</p>}
    <h2>{t('waitingForInspection')}</h2>
    {queue.data?.waiting.map(application => <article key={application.loanApplicationId}>
      <b>{application.borrowerName}</b>
      {hasPermission(permissions, 'inspections.create') && <button onClick={async () => {
        try { setSelected(await inspectionsApi.create(application.loanApplicationId, (await loanApplicationsApi.get(application.loanApplicationId)).eTag)); }
        catch (caught) { setError(caught); }
      }}>{t('createInspection')}</button>}
    </article>)}
    <h2>{t('existingInspections')}</h2>
    {queue.data?.existing.map(item => <article key={item.inspection.propertyInspectionId}>
      <b>{item.borrowerName}</b> — {t(item.inspection.status.toLowerCase())}
      <button onClick={() => setSelected(item.inspection)}>{t('openInspection')}</button>
    </article>)}
  </section>;
}

function InspectionForm({ initial, permissions, close }: { initial: Inspection; permissions: string[]; close: () => void }) {
  const { t } = useTranslation();
  const [inspection, setInspection] = useState(initial);
  const [values, setValues] = useState<InspectionInput>(initial);
  const [reason, setReason] = useState('');
  const save = useMutation({ mutationFn: () => inspectionsApi.edit(inspection, values), onSuccess: setInspection });
  const complete = useMutation({ mutationFn: () => inspectionsApi.complete(inspection), onSuccess: setInspection });
  const decide = useMutation({ mutationFn: (decision: 'approve' | 'reject') => inspectionsApi.decision(inspection, decision, reason), onSuccess: setInspection });
  const finalized = inspection.status === 'Approved' || inspection.status === 'Rejected';
  const error = save.error ?? complete.error ?? decide.error;
  const field = (key: keyof InspectionInput, type = 'text') => <label>{t(key)}<input type={type} value={values[key] ?? ''} disabled={finalized} onChange={event => setValues({ ...values, [key]: type === 'number' ? Number(event.target.value) : event.target.value })} /></label>;
  return <section>
    <button onClick={close}>{t('back')}</button><h1>{t('propertyInspection')}</h1><p>{t('status')}: {t(inspection.status.toLowerCase())}</p>
    <form onSubmit={(event: FormEvent) => { event.preventDefault(); save.mutate(); }}>
      {field('governorate')}{field('state')}{field('area')}{field('inspectionDate', 'date')}{field('numberOfFloors', 'number')}{field('numberOfRooms', 'number')}{field('propertyArea', 'number')}{field('propertyCondition')}{field('result')}
      <label>{t('notes')}<textarea value={values.notes ?? ''} disabled={finalized} onChange={event => setValues({ ...values, notes: event.target.value })} /></label>
      {!finalized && hasPermission(permissions, 'inspections.create') && <><button>{t('save')}</button>{inspection.status === 'Draft' && <button type="button" onClick={() => complete.mutate()}>{t('completeInspection')}</button>}</>}
    </form>
    {inspection.status === 'Recorded' && hasPermission(permissions, 'inspections.approve') && <div><label>{t('rejectionReason')}<textarea value={reason} onChange={event => setReason(event.target.value)} /></label><button onClick={() => decide.mutate('approve')}>{t('approve')}</button><button disabled={!reason.trim()} onClick={() => decide.mutate('reject')}>{t('reject')}</button></div>}
    {error !== undefined && <p role="alert">{t(errorKey(error))}</p>}
  </section>;
}
