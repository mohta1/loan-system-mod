import { FormEvent, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../api/identity';
import { loanProductsApi, Product, RankRule, Version, VersionInput } from '../api/loanProducts';

const blank = (): VersionInput => ({
  maximumAmount: 0,
  currency: '',
  deductionPercentage: 0,
  financingTypes: ['Purchase Existing House', 'Build New House'],
  eligibilityConfiguration: {
    requiredNationality: '',
    maximumApplicationCount: 0,
    rankGradeAmountRules: [{ rankGrade: '', maximumAmount: 0 }],
    term: { maximumTermMonths: 0, dueDateRule: '' },
  },
  effectiveFrom: new Date().toISOString().slice(0, 10),
  effectiveTo: null,
});

export function LoanProductsPage({ permissions }: { permissions: string[] }) {
  const { t } = useTranslation();
  const products = useQuery({ queryKey: ['loanProducts'], queryFn: loanProductsApi.list });
  const [selected, setSelected] = useState<string>();
  const [creating, setCreating] = useState(false);
  if (selected) return <ProductDetail id={selected} permissions={permissions} back={() => setSelected(undefined)} />;
  return <section>
    <div className="section-head"><h2>{t('loanProducts')}</h2>{permissions.includes('loanProducts.manage') && <button className="primary" onClick={() => setCreating(true)}>{t('createProduct')}</button>}</div>
    {products.isError ? <p role="alert">{t('loanProductError')}</p> : <table><thead><tr><th>{t('name')}</th><th>{t('status')}</th><th>{t('versions')}</th></tr></thead><tbody>{products.data?.map(product => <tr key={product.loanProductId} onClick={() => setSelected(product.loanProductId)}><td>{product.name}</td><td><span className={`badge ${product.status === 'Active' ? 'ok' : 'off'}`}>{t(product.status.toLowerCase())}</span></td><td>{product.versionCount}</td></tr>)}</tbody></table>}
    {creating && <CreateProduct close={() => setCreating(false)} />}
  </section>;
}

function CreateProduct({ close }: { close: () => void }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [name, setName] = useState('');
  const create = useMutation({ mutationFn: () => loanProductsApi.create(name), onSuccess: () => { void queryClient.invalidateQueries({ queryKey: ['loanProducts'] }); close(); } });
  return <div className="overlay"><form className="dialog" onSubmit={event => { event.preventDefault(); create.mutate(); }}><h3>{t('createProduct')}</h3><label>{t('name')}<input value={name} onChange={event => setName(event.target.value)} required /></label><footer><button type="button" onClick={close}>{t('cancel')}</button><button className="primary">{t('create')}</button></footer></form></div>;
}

function ProductDetail({ id, permissions, back }: { id: string; permissions: string[]; back: () => void }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const productQuery = useQuery({ queryKey: ['loanProduct', id], queryFn: () => loanProductsApi.get(id) });
  const [editing, setEditing] = useState<Version>();
  const [newDraft, setNewDraft] = useState(false);
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['loanProduct', id] });
  const publish = useMutation({ mutationFn: (version: Version) => loanProductsApi.publish(id, version), onSuccess: refresh });
  const status = useMutation({ mutationFn: (product: Product) => loanProductsApi.status(product, product.status !== 'Active'), onSuccess: refresh });
  if (!productQuery.data) return <section><button onClick={back}>{t('back')}</button><p>{t('loading')}</p></section>;
  const product = productQuery.data;
  return <section>
    <button onClick={back}>{t('back')}</button>
    <div className="section-head"><div><h2>{product.name}</h2><span className={`badge ${product.status === 'Active' ? 'ok' : 'off'}`}>{t(product.status.toLowerCase())}</span></div><div className="actions">{permissions.includes('loanProducts.manage') && <button onClick={() => setNewDraft(true)}>{t('createDraft')}</button>}{permissions.includes('loanProducts.manageStatus') && <button onClick={() => status.mutate(product)}>{t(product.status === 'Active' ? 'deactivate' : 'activate')}</button>}</div></div>
    {product.versions.map(version => <article className="product-version" key={version.versionId}>
      <h3>{t('version')} {version.versionNumber} <span className={`badge ${version.status === 'Published' ? 'ok' : 'off'}`}>{t(version.status.toLowerCase())}</span></h3>
      <dl><dt>{t('maximumAmount')}</dt><dd>{version.maximumAmount} {version.currency}</dd><dt>{t('deductionPercentage')}</dt><dd>{version.deductionPercentage}%</dd><dt>{t('effectivePeriod')}</dt><dd>{version.effectiveFrom} — {version.effectiveTo ?? '∞'}</dd><dt>{t('financingTypes')}</dt><dd>{version.financingTypes.join(', ')}</dd><dt>{t('requiredNationality')}</dt><dd>{version.eligibilityConfiguration.requiredNationality}</dd><dt>{t('maximumApplicationCount')}</dt><dd>{version.eligibilityConfiguration.maximumApplicationCount}</dd><dt>{t('rankGradeRules')}</dt><dd><ul>{version.eligibilityConfiguration.rankGradeAmountRules.map(rule => <li key={rule.rankGrade}>{rule.rankGrade} — {rule.maximumAmount} {version.currency}</li>)}</ul></dd><dt>{t('termMonths')}</dt><dd>{version.eligibilityConfiguration.term.maximumTermMonths}</dd><dt>{t('dueDateRule')}</dt><dd>{version.eligibilityConfiguration.term.dueDateRule}</dd></dl>
      {version.status === 'Draft' && permissions.includes('loanProducts.manage') && <button onClick={() => setEditing(version)}>{t('editDraft')}</button>}
      {version.status === 'Draft' && permissions.includes('loanProducts.publish') && <button className="primary" onClick={() => publish.mutate(version)}>{t('publish')}</button>}
    </article>)}
    {(editing || newDraft) && <VersionForm productId={id} version={editing} close={() => { setEditing(undefined); setNewDraft(false); }} done={() => { setEditing(undefined); setNewDraft(false); void refresh(); }} />}
    {(publish.isError || status.isError) && <p role="alert">{publish.error instanceof ApiError && publish.error.status === 412 ? t('loanProductConcurrency') : t('loanProductError')}</p>}
  </section>;
}

function VersionForm({ productId, version, close, done }: { productId: string; version?: Version; close: () => void; done: () => void }) {
  const { t } = useTranslation();
  const [input, setInput] = useState<VersionInput>(() => version ? structuredClone(version) : blank());
  const [financingType, setFinancingType] = useState('');
  const save = useMutation({ mutationFn: () => version ? loanProductsApi.edit(productId, version, input) : loanProductsApi.draft(productId, input), onSuccess: done });
  const set = (value: Partial<VersionInput>) => setInput(current => ({ ...current, ...value }));
  const setEligibility = (value: Partial<VersionInput['eligibilityConfiguration']>) => setInput(current => ({ ...current, eligibilityConfiguration: { ...current.eligibilityConfiguration, ...value } }));
  const setRules = (rules: RankRule[]) => setEligibility({ rankGradeAmountRules: rules });
  const updateRule = (index: number, value: Partial<RankRule>) => setRules(input.eligibilityConfiguration.rankGradeAmountRules.map((rule, current) => current === index ? { ...rule, ...value } : rule));
  const submit = (event: FormEvent) => { event.preventDefault(); save.mutate(); };
  return <div className="overlay"><form className="dialog product-form" onSubmit={submit}>
    <h3>{t(version ? 'editDraft' : 'createDraft')}</h3>
    <label>{t('maximumAmount')}<input type="number" min="0.0001" step="0.0001" value={input.maximumAmount} onChange={event => set({ maximumAmount: +event.target.value })} /></label>
    <label>{t('currency')}<input maxLength={3} value={input.currency} onChange={event => set({ currency: event.target.value })} /></label>
    <label>{t('deductionPercentage')}<input type="number" min="0" max="100" step="0.0001" value={input.deductionPercentage} onChange={event => set({ deductionPercentage: +event.target.value })} /></label>
    <label>{t('effectiveFrom')}<input type="date" value={input.effectiveFrom} onChange={event => set({ effectiveFrom: event.target.value })} /></label>
    <label>{t('effectiveTo')}<input type="date" value={input.effectiveTo ?? ''} onChange={event => set({ effectiveTo: event.target.value || null })} /></label>
    <fieldset><legend>{t('financingTypes')}</legend>{input.financingTypes.map(type => <button type="button" key={type} onClick={() => set({ financingTypes: input.financingTypes.filter(value => value !== type) })}>{type} ×</button>)}<input value={financingType} onChange={event => setFinancingType(event.target.value)} /><button type="button" onClick={() => { const value = financingType.trim(); if (value && !input.financingTypes.some(type => type.toLowerCase() === value.toLowerCase())) set({ financingTypes: [...input.financingTypes, value] }); setFinancingType(''); }}>{t('add')}</button></fieldset>
    <label>{t('requiredNationality')}<input value={input.eligibilityConfiguration.requiredNationality} onChange={event => setEligibility({ requiredNationality: event.target.value })} /></label>
    <label>{t('maximumApplicationCount')}<input type="number" min="1" value={input.eligibilityConfiguration.maximumApplicationCount} onChange={event => setEligibility({ maximumApplicationCount: +event.target.value })} /></label>
    <fieldset><legend>{t('rankGradeRules')}</legend>{input.eligibilityConfiguration.rankGradeAmountRules.map((rule, index) => <div className="rank-rule" key={index}><label>{t('rankGrade')}<input value={rule.rankGrade} onChange={event => updateRule(index, { rankGrade: event.target.value })} /></label><label>{t('rankAmount')}<input type="number" min="0.0001" step="0.0001" value={rule.maximumAmount} onChange={event => updateRule(index, { maximumAmount: +event.target.value })} /></label><button type="button" onClick={() => setRules(input.eligibilityConfiguration.rankGradeAmountRules.filter((_, current) => current !== index))}>{t('removeRankRule')}</button></div>)}<button type="button" onClick={() => setRules([...input.eligibilityConfiguration.rankGradeAmountRules, { rankGrade: '', maximumAmount: 0 }])}>{t('addRankRule')}</button></fieldset>
    <label>{t('termMonths')}<input type="number" min="1" value={input.eligibilityConfiguration.term.maximumTermMonths} onChange={event => setEligibility({ term: { ...input.eligibilityConfiguration.term, maximumTermMonths: +event.target.value } })} /></label>
    <label>{t('dueDateRule')}<input value={input.eligibilityConfiguration.term.dueDateRule} onChange={event => setEligibility({ term: { ...input.eligibilityConfiguration.term, dueDateRule: event.target.value } })} /></label>
    {save.isError && <p role="alert">{save.error instanceof ApiError && save.error.status === 412 ? t('loanProductConcurrency') : t('loanProductValidation')}</p>}
    <footer><button type="button" onClick={close}>{t('cancel')}</button><button className="primary">{t('save')}</button></footer>
  </form></div>;
}
