import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { borrowersApi } from '../api/borrowers';
import { loanApplicationsApi, LoanApplication } from '../api/loanApplications';
import { LoanAccount, loansApi } from '../api/loans';
import { hasPermission } from '../app/permissions';

const compactId = (value: string) => value.length <= 18 ? value : `${value.slice(0, 8)}…${value.slice(-5)}`;

function TechnicalReference({ value }: { value: string }) {
  return <small><code title={value}>{compactId(value)}</code></small>;
}

export function LoanAccountsPage({ permissions = [], onOpenApplication }: { permissions?: string[]; onOpenApplication?: (application: LoanApplication) => void }) {
  const { t } = useTranslation();
  const [selected, setSelected] = useState<LoanAccount | null>(null);
  const [search, setSearch] = useState('');
  const q = useQuery({ queryKey: ['loans', search], queryFn: () => loansApi.list(search ? `sourceApplicationId=${encodeURIComponent(search)}` : '') });

  if (selected) return <LoanDetails loan={selected} permissions={permissions} onOpenApplication={onOpenApplication} back={() => setSelected(null)} />;

  return <section>
    <header className="page-header"><h1>{t('loanAccounts')}</h1></header>
    <label>{t('sourceApplication')}<input value={search} onChange={e => setSearch(e.target.value)} placeholder={t('applicationId')} /></label>
    {q.isError && <p role="alert">{t('loanAccountError')}</p>}
    <div className="table-scroll"><table>
      <thead><tr><th>{t('loanId')}</th><th>{t('sourceApplication')}</th><th>{t('approvedAmount')}</th><th>{t('availableToDisburse')}</th><th>{t('status')}</th></tr></thead>
      <tbody>{q.data?.items.map(x => <tr key={x.loanId} onClick={() => setSelected(x)}>
        <td><code title={x.loanId}>{compactId(x.loanId)}</code></td>
        <td><code title={x.sourceApplicationId}>{compactId(x.sourceApplicationId)}</code></td>
        <td>{x.approvedAmount} {x.currency}</td>
        <td>{x.availableToDisburse} {x.currency}</td>
        <td>{t(x.status.toLowerCase())}</td>
      </tr>)}</tbody>
    </table></div>
  </section>;
}

function LoanDetails({ loan, permissions, onOpenApplication, back }: { loan: LoanAccount; permissions: string[]; onOpenApplication?: (application: LoanApplication) => void; back: () => void }) {
  const { t } = useTranslation();
  const q = useQuery({ queryKey: ['loan', loan.loanId], queryFn: () => loansApi.get(loan.loanId), initialData: loan });
  const canReadBorrower = hasPermission(permissions, 'borrowers.read');
  const canReadApplication = hasPermission(permissions, 'loanApplications.read');
  const borrower = useQuery({ queryKey: ['borrower', loan.borrowerId], queryFn: () => borrowersApi.get(loan.borrowerId), enabled: canReadBorrower });
  const application = useQuery({ queryKey: ['loanApplication', loan.sourceApplicationId], queryFn: () => loanApplicationsApi.get(loan.sourceApplicationId), enabled: canReadApplication });
  const x = q.data;

  return <section>
    <button onClick={back}>{t('back')}</button>
    <h1>{t('loanAccountDetails')}</h1>
    {q.isError ? <p role="alert">{t('loanAccountError')}</p> : <dl className="details-grid">
      <dt>{t('loanId')}</dt>
      <dd><TechnicalReference value={x.loanId} /></dd>

      <dt>{t('sourceApplication')}</dt>
      <dd>
        {application.data ? <>
          <strong>{application.data.productSnapshot.productName}</strong>
          <div>{t('financingType')}: {application.data.financingType} · {t('status')}: {t(application.data.status.toLowerCase())}</div>
          <div>{t('requestedAmount')}: {application.data.requestedAmount} {application.data.currency}</div>
          {onOpenApplication && <button type="button" className="link" onClick={() => onOpenApplication(application.data!)}>{t('viewApplication')}</button>}
        </> : null}
        <div>{t('applicationId')}: <TechnicalReference value={x.sourceApplicationId} /></div>
      </dd>

      <dt>{t('borrower')}</dt>
      <dd>
        {borrower.data ? <>
          <strong>{borrower.data.fullName}</strong>
          <div>{t('civilNumber')}: {borrower.data.civilNumber}</div>
          {borrower.data.employeeNumber && <div>{t('employeeNumber')}: {borrower.data.employeeNumber}</div>}
        </> : null}
        <TechnicalReference value={x.borrowerId} />
      </dd>

      <dt>{t('approvedAmount')}</dt><dd>{x.approvedAmount} {x.currency}</dd>
      <dt>{t('reservedAmount')}</dt><dd>{x.reservedDisbursementAmount} {x.currency}</dd>
      <dt>{t('totalDisbursed')}</dt><dd>{x.totalDisbursed} {x.currency}</dd>
      <dt>{t('availableToDisburse')}</dt><dd>{x.availableToDisburse} {x.currency}</dd>
      <dt>{t('totalRepaid')}</dt><dd>{x.totalRepaid} {x.currency}</dd>
      <dt>{t('outstandingBalance')}</dt><dd>{x.outstandingBalance} {x.currency}</dd>
      <dt>{t('status')}</dt><dd>{t(x.status.toLowerCase())}</dd>
    </dl>}
  </section>;
}
